# Tailwind CSS VS2022 Editor Support 

![Visual Studio Marketplace Downloads](https://img.shields.io/visual-studio-marketplace/i/TheronWang.TailwindCSSIntellisense)
![Visual Studio Marketplace Downloads](https://img.shields.io/visual-studio-marketplace/d/TheronWang.TailwindCSSIntellisense)
![Visual Studio Marketplace Downloads](https://img.shields.io/visual-studio-marketplace/r/TheronWang.TailwindCSSIntellisense)

![Visual Studio Marketplace Version (including pre-releases)](https://img.shields.io/visual-studio-marketplace/v/TheronWang.TailwindCSSIntellisense)

IntelliSense, linting, build shortcuts, and more for Tailwind CSS to enhance the development experience in Visual Studio 2022.

**NOTE**: this extension is designed to be used with Tailwind 3/4; there may be unintended effects when using earlier versions.

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

The extension will start in any v3 solution with a `tailwind.config.{js,cjs,mjs,ts,cts,mts}` file in any project.

If your configuration file is named differently/not found by the extension, you can set the file by right-clicking on it (must be a `.js` or `.ts` file).

In v4 projects, you must manually set your CSS configuration file in your `tailwind.extension.json` file, which can be updated by right-clicking on the desired file.

## Features

### IntelliSense

Tailwind CSS classes show up in the IntelliSense completion menus in Razor, HTML, and CSS files:

![Intellisense Demo](https://raw.githubusercontent.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/main/art/IntelliSense-Demo-1.gif)

### Linting

The linter will mark all class conflicts as well as invalid `theme()`, `screen()`, and `@tailwind` values:

![Linter](https://raw.githubusercontent.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/main/art/Linter.png)

Linter options can be found in Tools > Options > Tailwind CSS IntelliSense > Linter.

Extensions do not have the ability to override existing error tags, so `@tailwind`, `@apply`, and other Tailwind-specific functions will be underlined by Visual Studio. 

### Class Sorting

Classes can be sorted on a per-file basis on file save or by the whole solution on build:

![Class Sort Demo 1](https://raw.githubusercontent.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/main/art/class-sort-demo.gif)

You can also manually sort by clicking the Tools menu at the top and selecting a sort option:

![Class Sort Demo 2](https://raw.githubusercontent.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/main/art/Class-Sort-2.png)

### Build Integration

To build your Tailwind file, ensure that your input and output css files are defined. By default, a project build will trigger the Tailwind build process, but this can be manually triggered under the Build menu. Build details and errors will be logged to the Build output window:

![Build Demo 1](https://raw.githubusercontent.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/main/art/Build-Demo-1.png)
![Build Demo 2](https://raw.githubusercontent.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/main/art/Build-Demo-2.png)

Set your configuration, build and output files by right-clicking on any `.js`, `.ts`, or `.css` file:

![Customizability Build 1](https://raw.githubusercontent.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/main/art/Customizability-Build-1.png)
![Customizability Build 2](https://raw.githubusercontent.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/main/art/Customizability-Build-2.png)

In Tailwind v4, setting `.css` files as configuration files via the context menu will automatically add them to the build pipeline.

Your preferences will be stored in a `tailwind.extension.json` file in your project root.

### NPM Integration

When getting started in a new project, you can install the necessary modules and configure the extension by right-clicking on the project node:

![NPM Shortcut 1](https://raw.githubusercontent.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/main/art/NPM-Shortcuts-1.png)

**Using the standalone Tailwind CSS CLI:**
- Specify the absolute path in Tools > Options > Tailwind CSS IntelliSense > Tailwind CLI path.
- Click 'Set up Tailwind CSS (use CLI)'

If you need to override the default build command, `package.json` scripts are also supported. The extension will run the build as `npm run {your task}`, where the task can be defined in the extension settings.

### Extension Options

Extension settings are located in Tools > Options > Tailwind CSS IntelliSense.

## Troubleshooting

### Build

If you notice that your build file is not being updated, check the Build output window to see if you have any syntax errors:

![Build Error Output](https://raw.githubusercontent.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/main/art/Troubleshooting-Build.png)<br>

### Extension

If something doesn't work as expected or you see an exception message, you can check the detailed log in the Extensions output window.

## Support

To report issues or share feature suggestions, feel free to create an issue [on GitHub](https://github.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/issues/new).

If this extension has helped you, please consider [leaving a small donation](https://github.com/sponsors/theron-wang) to support development!