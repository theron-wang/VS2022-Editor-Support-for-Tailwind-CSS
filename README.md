# Tailwind CSS VS2022 Editor Support 

![Visual Studio Marketplace Downloads](https://img.shields.io/visual-studio-marketplace/d/TheronWang.TailwindCSSIntellisense)
![Visual Studio Marketplace Version (including pre-releases)](https://img.shields.io/visual-studio-marketplace/v/TheronWang.TailwindCSSIntellisense)

Editor features such as IntelliSense, build integration, among others for Tailwind CSS to enhance the development experience in Visual Studio 2022.

**NOTE**: this extension is designed to be used with the latest version of Tailwind; there may be unintended effects when using earlier versions.

Download from the [Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=TheronWang.TailwindCSSIntellisense).

To get started, view this [guide](https://github.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/blob/main/Getting-Started.md).

## Changelog

For information on recent updates, see [the changelog](https://github.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/blob/main/CHANGELOG.md).

## Disclaimer

This is **not** an official Tailwind CSS extension and is **not** affiliated with Tailwind Labs Inc. 

## Prerequisites

This extension uses `npm` and `node` under the hood, so you should have them installed to avoid errors.

To check if you have `npm` installed, run `npm -v` in the terminal.

If you do not have `npm` installed, follow [this tutorial](https://docs.npmjs.com/downloading-and-installing-node-js-and-npm) from the official npm docs.

## Usage

### IntelliSense

Tailwind CSS classes show up in the IntelliSense completion menus in Razor, HTML, and CSS files:

![Intellisense Demo](https://raw.githubusercontent.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/main/art/IntelliSense-Demo-1.gif)

Customization support for `theme` values and plugins (experimental):

![Custom Configuration File](https://raw.githubusercontent.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/main/art/IntelliSense-Demo-2-Configuration.png)

![Intellisense Demo 2](https://raw.githubusercontent.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/main/art/IntelliSense-Demo-3.png)

### Build Integration

When in a project, you can run the project to build Tailwind CSS or access it via the Build menu:

![Build Demo 1](https://raw.githubusercontent.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/main/art/Build-Demo-1.png)

In an open folder context, however, you must right click the folder instead to toggle the build process.

After the process has started, the process will continue to build and output data to the Build window pane:

![Build Demo 2](https://raw.githubusercontent.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/main/art/Build-Demo-2.png)

Custom `package.json` scripts are supported on build:

![NPM Package.json](https://raw.githubusercontent.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/main/art/NPM-package-json.png)

You can customize the behavior in Tools > Options > Tailwind CSS Intellisense > Custom Build.

### NPM Integration

When getting started in a new project, you can import the necessary modules by right-clicking on the project node:

![NPM Shortcut 1](https://raw.githubusercontent.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/main/art/NPM-Shortcuts-1.png)

### Customizability

#### Build

When using the default Tailwind build process, you can set the following:

If you want to explicitly state the configuration file, you can right click any JavaScript file as follows:

![Customizability Build 1](https://raw.githubusercontent.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/main/art/Customizability-Build-1.png)

Likewise, for the build file and the output file, you can also explicitly state which CSS files you want:

![Customizability Build 2](https://raw.githubusercontent.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/main/art/Customizability-Build-2.png)

If you right click on each file again, you will find a remove option to set it back to the default.

Please note that the extension creates a `tailwind.extension.json` file in your project root.

#### Extension Options

Extension settings are located in Tools > Options > Tailwind CSS Intellisense.

## Troubleshooting

### Build

If you notice that your build file is not being updated, check the Build output window to see if you have any syntax errors:

![Build Error Output](https://raw.githubusercontent.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/main/art/Troubleshooting-Build.png)<br>
*Build output modified for brevity*

### Extension

If something doesn't work as expected or you see an exception message, you can check the detailed log in the Extensions output window.

Please report any errors that you encounter!

## Limitations

Visual Studio does not provide an API to override error tagging, so you will see the CSS directives (`@apply`, `@tailwind`, etc.) marked as errors.

## Bugs / Suggestions

If you run into any issues or come up with any feature suggestions while using this extension, please create an issue [on the GitHub repo](https://github.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/issues/new).
