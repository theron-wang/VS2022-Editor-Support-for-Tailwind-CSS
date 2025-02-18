# Upgrade Guide (v3 -> v4)

Upgrading to Tailwind v4 requires a few changes to your `tailwind.extension.json` file. Changes are minimal, but they do require manual adjustments to ensure everything works as intended.

### 1. Upgrade Tailwind version

Follow the [offical upgrade guide](https://tailwindcss.com/docs/upgrade-guide) to upgrade your Tailwind version. The extension uses `@tailwindcss/cli` for building v4 projects, so make sure you have it installed.
```
npx @tailwindcss/upgrade
```
or
```
npm install tailwindcss@latest @tailwindcss/cli@latest
```

### 2. Update your `tailwind.extension.json` file

Remove any JavaScript configuration files and replace them with your input css files. Each configuration file should be defined in both `ConfigurationFiles` and `BuildFiles`.

Sample:

```json
{
  "$schema": "https://raw.githubusercontent.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/refs/heads/main/tailwind.extension.schema.json",
  "ConfigurationFiles": [
    {
      "Path": "tailwind.css",
      "IsDefault": true,
      "ApplicableLocations": []
    }
  ],
  "BuildFiles": [
    {
      "Input": "tailwind.css",
      "Output": "tailwind.output.css"
    }
  ],
  "PackageConfigurationFile": null,
  "CustomRegexes": {
    "Razor": {
      "Override": false,
      "Values": []
    },
    "HTML": {
      "Override": false,
      "Values": []
    },
    "JavaScript": {
      "Override": false,
      "Values": []
    }
  },
  "UseCli": false
}
```

If you still require the use of a JavaScript configuration file, simply add it to your input/config css file using `@config`:

```css
@import "tailwindcss";
@config "./path/to/your/config.js";
```

After making these changes, the extension should now work with v4. Please note: **A project reload/VS restart may be required to load all changes.**

A list of all supported v4 features (and those whose support will be added later) can be found in the `1.10.0` section of the [changelog](https://github.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/blob/main/CHANGELOG.md).