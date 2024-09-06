(function () {
    (async function () {
        var Module = require('module');
        var path = require('path');
        var originalRequire = Module.prototype.require;
        console.log(process.cwd());

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
                            }
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

        var configuration;

        let filePath = process.argv[2];

        if (!path.isAbsolute(filePath)) {
            filePath = path.join(process.cwd(), filePath);
        }

        try {
            configuration = require(filePath);
        } catch {
            configuration = await import(filePath);
        }

        if (configuration.default) {
            configuration = configuration.default;
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

        if (configuration.plugins) {
            var pluginTheme = { theme: configuration.theme };
            var newPlugins = [];
            configuration.plugins.reverse().forEach(function (plugin) {
                if (typeof plugin === 'function') {
                    try {
                        var evaluated = plugin({});

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
                        pluginTheme[key] = { ...pluginTheme[key], ...plugin.config[key] };
                    });

                    newPlugins.push(plugin.handler);
                } else {
                    newPlugins.push(plugin);
                }
            });

            configuration = {
                ...configuration,
                ...pluginTheme
            };

            configuration.plugins = newPlugins;
        }

        function theme(key, defaultValue) {
            const defaultTheme = require('tailwindcss/defaultTheme');
            const colors = require('tailwindcss/colors');
            const custom = configuration;

            let defaultThemeValue = getValueByKeyBracket(defaultTheme, key);

            // Default theme may contain functions, so we should provide that
            if (typeof defaultThemeValue === 'function') {
                console.log(theme);
                defaultThemeValue = defaultThemeValue({ theme: theme, colors: colors });
            }

            const output = {
                ...defaultThemeValue,
                ...getValueByKeyBracket(custom.theme, key),
                ...getValueByKeyBracket(custom.theme.extend, key)
            };

            return (!output || Object.keys(output).length === 0) ? defaultValue : output;
        }

        const defaultLog = console.log;
        console.log = function () { };

        function camelToKebab(camelCaseStr) {
            return camelCaseStr
                .replace(/([a-z])([A-Z])/g, '$1-$2')
                .toLowerCase();
        }

        var parsed = JSON.stringify(configuration,
            (key, value) => {
                if (key === 'plugins') {
                    var classes = [];
                    var modifiers = [];
                    var customDescripts = [];
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
                                            classes.push(...Object.keys(u));
                                            customDescripts.push(...Object.entries(u));
                                        });
                                    } else {
                                        classes.push(...Object.keys(utilities));
                                        customDescripts.push(...Object.entries(utilities));
                                    }
                                }
                            },
                            matchUtilities: (utilities, { values, supportsNegativeValues } = null) => {
                                if (utilities) {
                                    if (values) {
                                        for (const v of Object.entries(values)) {
                                            for (const u of Object.entries(utilities)) {
                                                var input = `${u[0]}-${v[0]}`.replace('-DEFAULT', '');
                                                classes.push(input);
                                                customDescripts[input] = u[1](v[1]);
                                                if (supportsNegativeValues === true) {
                                                    classes.push(`-${input}`);
                                                    let negated = { ...u[1](v[1]) };

                                                    for (const [key, value] of Object.entries(negated)) {
                                                        if (typeof value === 'number') {
                                                            negated[key] = -Math.abs(value);
                                                        } else if (typeof value === 'string' && /\d/.test(value)) {
                                                            negated[key] = value.replace(/(?:^|\s)\d/, (match) => {
                                                                const num = parseInt(match, 10);
                                                                return -Math.abs(num);
                                                            });
                                                        }
                                                    }

                                                    customDescripts[`-${input}`] = negated;
                                                }
                                            }
                                        }
                                    }

                                    for (const u of Object.entries(utilities)) {
                                        classes.push(`${u[0]}-[]`);
                                        if (supportsNegativeValues === true) {
                                            classes.push(`-${u[0]}-[]`);
                                        }
                                    }
                                }
                            },
                            addComponents: (components, options = null) => {
                                if (components) {
                                    if (typeof components[Symbol.iterator] === 'function') {
                                        components.forEach(function (c) {
                                            classes.push(...Object.keys(c));
                                            customDescripts.push(...
                                                Object.entries(c)
                                                    .map(([key, val]) => [
                                                        camelToKebab(key),
                                                        Object.fromEntries(Object.entries(val)
                                                            .filter(([_, v]) => typeof v === 'string' || typeof v === 'number'))
                                                    ]
                                                    ));
                                        })
                                    } else {
                                        classes.push(...Object.keys(components));
                                        customDescripts.push(...
                                            Object.entries(components)
                                                .map(([key, val]) => [
                                                    camelToKebab(key),
                                                    Object.fromEntries(Object.entries(val)
                                                        .filter(([_, v]) => typeof v === 'string' || typeof v === 'number'))
                                                ]
                                                ));
                                    }
                                }
                            },
                            matchComponents: (components, { values, supportsNegativeValues } = null) => {
                                if (components) {
                                    if (values) {
                                        for (const v of Object.entries(values)) {
                                            for (const u of Object.entries(components)) {
                                                var input = `${u[0]}-${v[0]}`.replace('-DEFAULT', '');
                                                classes.push(input);

                                                let convertedToKebab = Object.fromEntries(Object.entries(u[1](v[1]))
                                                    .filter(([_, v]) => typeof v === 'string' || typeof v === 'number')
                                                    .map(([key, val]) => [
                                                        camelToKebab(key),
                                                        val
                                                    ]));

                                                customDescripts[input] = convertedToKebab;
                                                if (supportsNegativeValues === true) {
                                                    classes.push(`-${input}`);

                                                    let negated = { ...convertedToKebab };

                                                    for (const [key, value] of Object.entries(negated)) {
                                                        if (typeof value === 'number') {
                                                            negated[key] = -Math.abs(value);
                                                        } else if (typeof value === 'string' && /\d/.test(value)) {
                                                            negated[key] = value.replace(/(?:^|\s)\d/, (match) => {
                                                                const num = parseInt(match, 10);
                                                                return -Math.abs(num);
                                                            });
                                                        }
                                                    }

                                                    customDescripts[`-${input}`] = negated;
                                                }
                                            }
                                        }
                                    }

                                    for (const u of Object.entries(components)) {
                                        classes.push(`${u[0]}-[]`);
                                        if (supportsNegativeValues === true) {
                                            classes.push(`-${u[0]}-[]`);
                                        }
                                    }
                                }
                            },
                            addBase: (base) => {
                                return;
                            },
                            addVariant: (name, value) => {
                                modifiers.push(name)
                            },
                            matchVariant: (name, cb, { values } = null) => {
                                if (name !== '@') {
                                    name += '-';
                                }
                                if (values) {
                                    for (const v of Object.entries(values)) {
                                        modifiers.push(`${name}${v[0].replace('DEFAULT', '')}`);
                                    }
                                }

                                modifiers.push(`${name}[]`);
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

                    customDescripts = Object.fromEntries(
                        Object.entries(customDescripts).map(([key, value]) => {
                            return Array.isArray(value) ?
                                [
                                    value[0].replace(".", ""),
                                    Object.entries(value[1]).map(item => {
                                        return typeof item === 'string'
                                            ? item
                                            : `${item[0]}: ${item[1]}`
                                    }
                                    ).join('; ') + ";"
                                ] : [
                                    key,
                                    typeof value === 'string'
                                        ? value
                                        : Object.entries(value)
                                            .filter(([_, v]) => Object.keys(v).length !== 0)
                                            .map(([k, v]) => `${k}: ${v}`)
                                            .join('; ') + ";"
                                ]
                        })
                    );

                    return {
                        'classes': classes,
                        'modifiers': modifiers,
                        'descriptions': customDescripts
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
    })()
})();