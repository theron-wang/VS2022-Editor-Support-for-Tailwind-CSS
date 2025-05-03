# Tailwind CSS VS2022 Editor Support 

![Visual Studio Marketplace Installs](https://img.shields.io/visual-studio-marketplace/i/TheronWang.TailwindCSSIntellisense) ![Visual Studio Marketplace Rating](https://img.shields.io/visual-studio-marketplace/r/TheronWang.TailwindCSSIntellisense) ![Visual Studio Marketplace Version (including pre-releases)](https://img.shields.io/visual-studio-marketplace/v/TheronWang.TailwindCSSIntellisense)

Bring IntelliSense, linting, class sorting, build tools, and more to Tailwind CSS development in Visual Studio 2022.

> **Note**: This extension best supports the latest versions of Tailwind CSS (v3, v4, and v4.1).

- [Download from the Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=TheronWang.TailwindCSSIntellisense)

- [Getting Started Guide](https://github.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/blob/main/Getting-Started.md)

## Changelog

For information on recent updates, see [the changelog](https://github.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/blob/main/CHANGELOG.md).

## Disclaimer

This is **not** an official Tailwind CSS extension and has **no affiliation** with Tailwind Labs Inc. 

## Prerequisites

This extension uses `npm` and `node`, so you should have them installed.

To check if you have `npm` installed, run `npm -v` in the terminal.

If you do not have `npm` installed, follow the [official install guide](https://docs.npmjs.com/downloading-and-installing-node-js-and-npm) from the official npm docs.

## Setup

The extension activates when:
- Your solution has a `tailwind.config.{js,cjs,mjs,ts,cts,mts}` file (for v3), or
- You're using Tailwind v4 and importing it in a `.css` file (`@import "tailwindcss"`)

If your config file isn't detected, right-click it in Solution Explorer → **Set as configuration file**.

## Features

### IntelliSense

Get Tailwind class suggestions in Razor, HTML, and CSS files:

![Intellisense Demo](https://raw.githubusercontent.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/main/art/IntelliSense-Demo-1.gif)

### Linting

Automatically flags:
- Conflicting classes
- Invalid `theme()`, `screen()`, or `@tailwind` usage

Note: Visual Studio might still flag some Tailwind features like `@apply` as errors—extensions can't override these tags.

![Linter](https://raw.githubusercontent.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/main/art/Linter.png)

### Class Sorting

Sort Tailwind classes:
- Automatically on save or build
- Manually from the **Tools** menu

![Class Sort Demo 1](https://raw.githubusercontent.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/main/art/class-sort-demo.gif)
![Class Sort Demo 2](https://raw.githubusercontent.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/main/art/Class-Sort-2.png)

### Build Integration

The extension can build your Tailwind CSS output when you build your project (or manually from the **Build** menu).
- Make sure your input/output CSS files are defined
- Output and errors appear in the Build output window

![Build Demo 1](https://raw.githubusercontent.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/main/art/Build-Demo-1.png)
![Build Demo 2](https://raw.githubusercontent.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/main/art/Build-Demo-2.png)

To configure build and configuration files, right-click `.js`, `.ts`, or `.css` files:

![Customizability Build 1](https://raw.githubusercontent.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/main/art/Customizability-Build-1.png)
![Customizability Build 2](https://raw.githubusercontent.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/main/art/Customizability-Build-2.png)

Settings are saved in a `tailwind.extension.json` file in your project root.

### NPM Integration

Start quickly by right-clicking your project and selecting a startup task:

![NPM Shortcut 1](https://raw.githubusercontent.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/main/art/NPM-Shortcuts-1.png)

Using the Tailwind CLI?
- Set its path under **Tools > Options > Tailwind CSS IntelliSense > Tailwind CLI path**
- Click **Set up Tailwind CSS (use CLI)**

Want to use a custom build script?
- Define it in your `package.json`
- Set the script name in the extension options (`npm run your-script-name`)

### Extension Options

Find global extension settings in:

> **Tools > Options > Tailwind CSS IntelliSense**

More details: [Getting Started – Extension Configuration](https://github.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/blob/main/Getting-Started.md#extension-configuration)

## Troubleshooting

### Build Issues

If your CSS isn't updating:
- Check the **Build output** window for Tailwind errors.

![Build Error Output](https://raw.githubusercontent.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/main/art/Troubleshooting-Build.png)<br>

### Extension Issues

If the extension crashes or behaves unexpectedly:
- Check the Extensions output window for detailed logs.

## Support

To report issues or share feature suggestions, feel free to create an issue [on GitHub](https://github.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/issues/new).

If this extension has helped you, please consider [leaving a small donation](https://github.com/sponsors/theron-wang) to support development!