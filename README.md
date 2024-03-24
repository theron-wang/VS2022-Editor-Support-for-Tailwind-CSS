# Tailwind CSS VS2022 Editor Support 

![Visual Studio Marketplace Downloads](https://img.shields.io/visual-studio-marketplace/d/TheronWang.TailwindCSSIntellisense)
![Visual Studio Marketplace Version (including pre-releases)](https://img.shields.io/visual-studio-marketplace/v/TheronWang.TailwindCSSIntellisense)

IntelliSense, linting, build shortcuts, and more for Tailwind CSS to enhance the development experience in Visual Studio 2022.

**NOTE**: this extension is designed to be used with the latest version of Tailwind; there may be unintended effects when using earlier versions.

Download from the [Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=TheronWang.TailwindCSSIntellisense).

To get started, view [the Getting Started guide](https://github.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/blob/main/Getting-Started.md).

## Changelog

For information on recent updates, see [the changelog](https://github.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/blob/main/CHANGELOG.md).

## Disclaimer

This is **not** an official Tailwind CSS extension and is **not** affiliated with Tailwind Labs Inc. 

## Prerequisites

This extension uses `npm` and `node` under the hood, so you should have them installed to avoid errors.

To check if you have `npm` installed, run `npm -v` in the terminal.

If you do not have `npm` installed, follow [this tutorial](https://docs.npmjs.com/downloading-and-installing-node-js-and-npm) from the official npm docs.

## Setup

The extension will start in any solution with a `tailwind.config.js` file in any project.

If your configuration file is named differently, or it is not found by the extension, you can set the file by right-clicking on it (must be a `.js` or `.ts` file).

Using TypeScript files will download the `ts-node` module globally for extension features to work. If this is not desirable, use `.js` files instead.

## Features

### IntelliSense

Tailwind CSS classes show up in the IntelliSense completion menus in Razor, HTML, and CSS files:

![Intellisense Demo](https://raw.githubusercontent.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/main/art/IntelliSense-Demo-1.gif)

*Due to limitations of Visual Studio, `.tsx` and `.jsx` files do not support Tailwind CSS IntelliSense.*

### Linting

The linter will mark all class conflicts as well as invalid `theme()`, `screen()`, and `@tailwind` values:

![Linter](https://raw.githubusercontent.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/main/art/Linter.png)

Linter options can be found in Tools > Options > Tailwind CSS IntelliSense > Linter.

Extensions do not have the ability to override existing error tags, so `@tailwind`, `@apply`, and other Tailwind-specific functions will be underlined by Visual Studio. 

### Build Integration

When in a project, you can run the project to build Tailwind CSS or access it via the Build menu. Build details and errors will be logged to the Build output window:

![Build Demo 1](https://raw.githubusercontent.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/main/art/Build-Demo-1.png)
![Build Demo 2](https://raw.githubusercontent.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/main/art/Build-Demo-2.png)

Set your configuration, build and output files by right-clicking on any `.js` or `.css` file:

![Customizability Build 1](https://raw.githubusercontent.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/main/art/Customizability-Build-1.png)
![Customizability Build 2](https://raw.githubusercontent.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/main/art/Customizability-Build-2.png)

Your preferences will be stored in a `tailwind.extension.json` file in your project root.

### NPM Integration

When getting started in a new project, you can import the necessary modules by right-clicking on the project node:

![NPM Shortcut 1](https://raw.githubusercontent.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/main/art/NPM-Shortcuts-1.png)

**Using the standalone Tailwind CSS CLI:**
- Specify the absolute path in Tools > Options > Tailwind CSS IntelliSense > Tailwind CLI path.
- Click 'Set up Tailwind CSS (use CLI)'

If you prefer a different build command, `package.json` scripts are also supported:

![NPM Package.json](https://raw.githubusercontent.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/main/art/NPM-package-json.png)

### Extension Options

Extension settings are located in Tools > Options > Tailwind CSS IntelliSense.

## Troubleshooting

### Build

If you notice that your build file is not being updated, check the Build output window to see if you have any syntax errors:

![Build Error Output](https://raw.githubusercontent.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/main/art/Troubleshooting-Build.png)<br>

### Extension

If something doesn't work as expected or you see an exception message, you can check the detailed log in the Extensions output window.

Please report any errors that you encounter!

## Bugs / Suggestions

If you run into any issues or come up with any feature suggestions while using this extension, please create an issue [on the GitHub repo](https://github.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/issues/new).
