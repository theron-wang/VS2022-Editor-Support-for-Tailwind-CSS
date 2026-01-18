(async function () {
    const Module = require('module');
    const path = require('path');
    const fs = require('fs/promises');
    const { pathToFileURL } = require('url');
    const { execSync } = require('child_process');
    const os = require('os');
    const originalRequire = Module.prototype.require;

    Module.prototype.require = function () {
        if (arguments[0] === 'tailwindcss/plugin') {
            return (function () {
                function withOptions(pluginDetails, pluginExports) {
                    return function (value) {
                        return {
                            handler: function (functions) {
                                options = value;
                                return pluginDetails(value)(functions);
                            },
                            config: (pluginExports && typeof pluginExports === 'function') ? pluginExports(value) : {}
                        };
                    };
                }
                function main(setup, configuration = {}) {
                    return function (value) {
                        return {
                            handler: function (functions) {
                                return setup(functions);
                            },
                            config: configuration
                        };
                    };
                }
                main.withOptions = withOptions;
                return main;
            })();
        }

        return originalRequire.apply(this, arguments);
    };

    let filePath = process.argv[2];
    const isPlugin = process.argv.includes('--plugin');

    if (!path.isAbsolute(filePath)) {
        filePath = path.join(process.cwd(), filePath);
    }

    function isTypeScriptFile(file) {
        return file.endsWith('.ts') || file.endsWith('.cts') || file.endsWith('.mts');
    }

    let configuration;

    if (isTypeScriptFile(filePath)) {
        const tempDir = await fs.mkdtemp(path.join(os.tmpdir(), 'ts-compile-'));
        const ext = path.extname(filePath);
        const outFile = path.join(tempDir, path.basename(filePath, ext) + '.js');

        try {
            execSync(
                `tsc "${filePath}" --outDir "${tempDir}" --module NodeNext --target ES2020 --esModuleInterop --allowJs --isolatedModules --moduleResolution nodenext`,
                { stdio: 'inherit' }
            );

            const imported = await import(pathToFileURL(outFile).href);
            configuration = imported.default || imported;
        } finally {
            try {
                await fs.rm(tempDir, { recursive: true, force: true });
            } catch {
                // ignore cleanup errors
            }
        }
    } else {
        try {
            configuration = require(filePath);
        } catch {
            const fileUrl = pathToFileURL(filePath).href;
            configuration = await import(fileUrl);
        }

        if (configuration.default) {
            configuration = configuration.default;
        }
    }

    if (isPlugin) {
        // Support v4 @plugin plugins: this way we don't have to rewrite this whole script
        // This is mainly so methods get passed in when parsing (preventing errors for
        // `addUtilities` is not a method, and so on)
        configuration = { plugins: [configuration] };
    }

    function getValueByKeyBracket(object, key) {
        const keys = key.split('.');

        const result = keys.reduce((acc, currentKey) => {
            if (acc && typeof acc === 'object' && currentKey in acc) {
                return acc[currentKey];
            }
            return undefined;
        }, object);

        return result;
    }

    function mergeDeep(target, source) {
        for (const key of Object.keys(source)) {
            if (source[key] instanceof Object && key in target) {
                target[key] = mergeDeep(target[key], source[key]);
            } else {
                target[key] = source[key];
            }
        }
        return target;
    }

    if (configuration.plugins) {
        let pluginTheme = { theme: configuration.theme };
        let pluginCorePlugins = {};
        let newPlugins = [];
        configuration.plugins.reverse().forEach(function (plugin) {
            if (typeof plugin === 'function') {
                try {
                    let evaluated = plugin({});

                    if (evaluated && evaluated.handler && evaluated.config) {
                        plugin = evaluated;
                    }
                } catch {

                }
            }
            if (plugin && plugin.handler && plugin.config) {
                if (!pluginTheme) {
                    pluginTheme = {};
                }

                Object.keys(plugin.config).forEach(key => {
                    pluginTheme[key] = mergeDeep(pluginTheme[key] || {}, plugin.config[key]);
                });

                if (plugin.config.corePlugins) {
                    if (Array.isArray(plugin.config.corePlugins)) {
                        pluginCorePlugins = plugin.config.corePlugins;
                    } else if (!Array.isArray(pluginCorePlugins)) {
                        pluginCorePlugins = { ...pluginCorePlugins, ...plugin.config.corePlugins };
                    }
                }

                newPlugins.push(plugin.handler);
            } else {
                newPlugins.push(plugin);
            }
        });

        const originalCorePlugins = configuration.corePlugins;

        configuration = {
            ...configuration,
            ...pluginTheme
        };

        configuration.plugins = newPlugins;

        if (!originalCorePlugins && !Array.isArray(originalCorePlugins)) {
            configuration.corePlugins = pluginCorePlugins;
        } else {
            configuration.corePlugins = originalCorePlugins;
        }
    }

    if (configuration.content?.files) {
        configuration.content = configuration.content.files;
    }

    if (configuration.content && Array.isArray(configuration.content)) {
        configuration.content = configuration.content.filter(item => typeof item === 'string');
    }

    function theme(key, defaultValue) {
        const defaultTheme = require('tailwindcss/defaultTheme');
        const colors = require('tailwindcss/colors');
        const custom = configuration;

        let defaultThemeValue = getValueByKeyBracket(defaultTheme, key);

        // Default theme may contain functions, so we should provide that
        if (typeof defaultThemeValue === 'function') {
            defaultThemeValue = defaultThemeValue({ theme: theme, colors: colors });
        }

        // In order of least to most precedence: default overridden by theme, theme overridden by extend
        const candidates = [
            defaultThemeValue,
            getValueByKeyBracket(custom.theme, key),
            getValueByKeyBracket(custom.theme?.extend, key)
        ].filter(v => Boolean(v));

        let output;
        if (candidates.every(c => typeof c !== "object")) {
            output = candidates[0];
        } else {
            // Is there ever a case where candidates are mixed in string/object/etc?
            // For now, assume not
            output = {
                ...candidates
                    .filter(c => typeof c === "object")
                    .reduce((acc, cur) => ({ ...acc, ...cur }), {})
            };
        }

        return (!output || Object.keys(output).length === 0) ? defaultValue : output;
    }

    const defaultLog = console.log;
    console.log = function () { };

    function camelToKebab(camelCaseStr) {
        return camelCaseStr
            .replace(/([a-z])([A-Z])/g, '$1-$2')
            .toLowerCase();
    }

    function formatDescriptionAsString(description) {
        // description itself is an object; if any values are also an object, then it is a nested rule. For example,
        // if a key is @layer a and the value is { color: "blue" }, then color is nested within @layer a

        let result = "";

        for (const [key, value] of Object.entries(description)) {
            if (typeof value === 'object' && value) {
                result += `${key} { ${formatDescriptionAsString(value)} } `;
            } else {
                result += `${camelToKebab(key)}: ${value}; `;
            }
        }

        return result.trim();
    }

    // Handle evaluated match objects (i.e., the result of a matchComponents or matchUtilities call)
    function handleEvaluatedMatchObject(evaluatedObject) {
        // See https://github.com/tailwindlabs/tailwindcss-aspect-ratio/blob/master/src/index.js#L39
        // I think the first object is always the description? Subsequent are for children
        // i.e., for .aspect-ratio, first is for itself, second could be for .aspect-ratio > *
        return Object.fromEntries(Object.entries(Array.isArray(evaluatedObject) ? evaluatedObject[0] : evaluatedObject));
    }

    // A valid class starts with a period, followed by a combination of letters, numbers, hyphens, underscores, Unicode characters,
    // and all else escaped.
    function isValidClass(className) {
        if (!className.startsWith('.')) {
            return false;
        }
        
        for (let i = 1; i < className.length; i++) {
            const char = className[i];

            // Check if non-ASCII: https://stackoverflow.com/questions/14313183/javascript-regex-how-do-i-check-if-the-string-is-ascii-only
            // If so, this is allowed
            if (/[^\x00-\x7F]/.test(char)) {
                continue;
            }

            // Check if alphanumeric, hyphen, or underscore
            if (/[a-zA-Z0-9\-\_]/.test(char)) {
                continue;
            }

            // All else must be escaped with a backslash
            // Note: this is a heuristic; there will be invalid cases with this approach
            // i.e., .hello\\\world is not valid, but most people won't be doing this
            if (char === '\\') {
                continue;
            } else if (className[i - 1] === '\\') {
                continue;
            }

            return false;
        }

        return true;
    }

    let parsed = JSON.stringify(configuration,
        (key, value) => {
            if (key === 'plugins') {
                let classes = new Set();
                let variants = new Set();
                let customDescripts = new Map();

                function addClasses(classesToAdd) {
                    if (!Array.isArray(classesToAdd)) {
                        classesToAdd = [classesToAdd];
                    }

                    while (classesToAdd.length > 0) {
                        const className = classesToAdd.pop();
                        if (className.includes(' ')) {
                            // Each may be comma separated
                            classesToAdd.push(...className.split(' ').map(c => c.endsWith(',') ? c.slice(0, -1) : c));
                            continue;
                        }

                        if (!isValidClass(className)) {
                            continue;
                        }

                        classes.add(className);
                    }
                }

                // Object.entries() of class name (with leading .) and description object
                // Therefore, it must be an array with an array (i.e., [['.class-name', { color: 'red' }], ...])
                function addDescriptions(descsToAdd) {
                    while (descsToAdd.length > 0) {
                        const [className, desc] = descsToAdd.pop();

                        if (className.includes(' ')) {
                            // Each may be comma separated
                            descsToAdd.push(...className.split(' ').map(c => c.endsWith(',') ? c.slice(0, -1) : c).map(c => [c, desc]));
                        }

                        if (!isValidClass(className)) {
                            continue;
                        }

                        const existing = customDescripts.get(className);

                        if (existing) {
                            customDescripts.set(className, {...existing, ...desc});
                        } else {
                            customDescripts.set(className, desc);
                        }
                    }
                }

                // Base of matchUtilities and matchComponents
                function matchImpl(compsOrUtils, { values, supportsNegativeValues } = null) {
                    if (compsOrUtils) {
                        if (values) {
                            for (const v of Object.entries(values)) {
                                for (const template of Object.entries(compsOrUtils)) {
                                    let input = `.${template[0]}-${v[0]}`.replace('-DEFAULT', '');

                                    addClasses(input);

                                    addDescriptions([[input, handleEvaluatedMatchObject(template[1](v[1]))]]);

                                    if (supportsNegativeValues === true) {
                                        let negativeValue = null;
                                        if (typeof v[1] === "number") {
                                            negativeValue = -v[1];
                                        } else if (typeof value === 'string' && /\d/.test(value)) {
                                            negativeValue = value.replace(/(?:^|\s)\d/, (match) => {
                                                const num = parseInt(match, 10);
                                                return -num;
                                            });
                                        }

                                        if (negativeValue !== null) {
                                            addClasses(`-${input}`);
                                            addDescriptions([[input, handleEvaluatedMatchObject(template[1](negativeValue))]]);
                                        }
                                    }
                                }
                            }
                        }

                        for (const u of Object.entries(compsOrUtils)) {
                            addClasses(`${u[0]}-[]`);
                            if (supportsNegativeValues === true) {
                                addClasses(`-${u[0]}-[]`);
                            }
                        }
                    }
                }

                value.forEach(function (p) {
                    p({
                        theme: theme,
                        config: (key, defaultValue) => {
                            return getValueByKeyBracket(configuration, key) || defaultValue;
                        },
                        addUtilities: (utilities, options = null) => {
                            if (utilities) {
                                if (typeof utilities[Symbol.iterator] === 'function') {
                                    utilities.forEach(function (u) {
                                        addClasses(Object.keys(u));
                                        addDescriptions(Object.entries(u));
                                    });
                                } else {
                                    addClasses(Object.keys(utilities));
                                    addDescriptions(Object.entries(utilities));
                                }
                            }
                        },
                        matchUtilities: matchImpl,
                        addComponents: (components, options = null) => {
                            if (components) {
                                if (typeof components[Symbol.iterator] === 'function') {
                                    components.forEach(function (c) {
                                        addClasses(Object.keys(c));
                                        addDescriptions(Object.entries(c));
                                    })
                                } else {
                                    addClasses(Object.keys(components));
                                    addDescriptions(Object.entries(components));
                                }
                            }
                        },
                        matchComponents: matchImpl,
                        addBase: (base) => {
                            return;
                        },
                        addVariant: (name, value) => {
                            variants.add(name)
                        },
                        matchVariant: (name, cb, { values } = null) => {
                            if (name !== '@') {
                                name += '-';
                            }
                            if (values) {
                                for (const v of Object.entries(values)) {
                                    variants.add(`${name}${v[0].replace('DEFAULT', '')}`);
                                }
                            }

                            variants.add(`${name}[]`);
                        },
                        corePlugins: (path) => {
                            return configuration.corePlugins[path] !== false;
                        },
                        e: (className) => {
                            return className.replace(/[!@#$%^&*(),.?":{}|<> ]/g, '\\$&');
                        },
                        prefix: (className) => {
                            return '.' + (configuration.prefix ? '' : configuration.prefix) + className.replace('.', '');
                        },
                        addDefaults: (className) => {
                            return;
                        }
                    });
                });

                // Remove any non-classes (may be :where(...) or @keyframes)
                classes = [...classes].filter(c => c.startsWith('.'));

                // Ensure that there are no descriptions that have an unmatched class, and format
                // the descriptions
                customDescripts = Object.fromEntries(
                    Array.from(customDescripts)
                        .filter(([className]) => classes.includes(className))
                        .map(([className, desc]) => [className.replace(".", ""), formatDescriptionAsString(desc)])
                );

                return {
                    'classes': classes,
                    'variants': [...variants],
                    'descriptions': customDescripts,
                    'content': configuration.content
                };
            } else {
                return typeof value === 'function' ? value({
                    theme: theme
                }) : value;
            }
        }
    );
    console.log = defaultLog;
    console.log(parsed);
})();