# Tailwind CSS VS2022 Editor Support 

![Visual Studio Marketplace Downloads](https://img.shields.io/visual-studio-marketplace/d/TheronWang.TailwindCSSIntellisense)
![Visual Studio Marketplace Version (including pre-releases)](https://img.shields.io/visual-studio-marketplace/v/TheronWang.TailwindCSSIntellisense)

Editor features such as IntelliSense, build integration, among others for Tailwind CSS to enhance the development experience in Visual Studio 2022.

**NOTE**: this extension is designed to be used with Tailwind v3.0.0+; there may be unintended effects when using earlier versions.

Download from the [Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=TheronWang.TailwindCSSIntellisense).

## Changelog

For information on recent updates, see [the changelog](https://github.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/blob/main/CHANGELOG.md).

## Disclaimer

This is NOT an official Tailwind CSS extension and is NOT affiliated with Tailwind Labs Inc. 

## Prerequisites

This extension uses npm for multiple features, so you should have npm installed to avoid errors.

To check if you have npm installed, run `npm -v` in the terminal.

If you do not have npm installed, follow [this tutorial](https://docs.npmjs.com/downloading-and-installing-node-js-and-npm) from the official npm docs.

## Usage

### IntelliSense

Tailwind CSS classes show up in the IntelliSense completion menus in Razor, HTML, and CSS files:

![Intellisense Demo](https://raw.githubusercontent.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/main/art/IntelliSense-Demo-1.gif)

Customization support for most `theme` and `theme.extension` values:

![Custom Configuration File](https://raw.githubusercontent.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/main/art/IntelliSense-Demo-2-Configuration.png)

![Intellisense Demo 2](https://raw.githubusercontent.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/main/art/IntelliSense-Demo-3.png)

See [this list](https://github.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/blob/main/ConfigSupported.md) to see what is and is not currently supported.

### Build Integration

In a project, simply run the project to build Tailwind CSS or access it via the Build menu:

![Build Demo 1](https://raw.githubusercontent.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/main/art/Build-Demo-1.png)

After the process has started, the process will continue to build and output data to the Build window pane:

![Build Demo 2](https://raw.githubusercontent.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/main/art/Build-Demo-2.png)

### NPM Integration

When getting started in a new project, you can import the necessary modules by right-clicking on the project node:

![NPM Shortcut 1](https://raw.githubusercontent.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/main/art/NPM-Shortcuts-1.png)

Custom package.json scripts are also supported on build:

![NPM Package.json](https://raw.githubusercontent.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/main/art/NPM-package-json.png)

You can customize the behavior in Tools > Options > TailwindCSS IntelliSense > Custom Build.

### Customizability

#### Build

When using the default Tailwind build process, you can set the following:

If you want to explicitly state the configuration file, you can right click any JavaScript file as follows:

![Customizability Build 1](https://raw.githubusercontent.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/main/art/Customizability-Build-1.png)

Likewise, for the build file and the output file, you can also explicitly state what CSS files you want:

![Customizability Build 2](https://raw.githubusercontent.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/main/art/Customizability-Build-2.png)

If you click on each file again, you will find a remove option to set it back to the default.

Please note that if you click any of these buttons, a tailwind.extension.json file will be created in your project root.

#### Extension Options

Extension settings are located in Tools > Options > TailwindCSS Intellisense.

## Troubleshooting

### Build

If you notice that your build file is not being updated, check the Build output window to see if you have any syntax errors:

![Build Error Output](https://raw.githubusercontent.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/main/art/Troubleshooting-Build.png)<br>
*Build output modified for brevity*

### Extension

If something doesn't work as expected or you see an exception message, you can check the detailed log in the Extensions output window.

Please report any errors that you encounter!

## Limitations

As of right now, Visual Studio does not provide an API to override error tagging, so you will see the CSS directives (`@apply`, `@tailwind`, etc.) marked as errors.

## Bugs / Suggestions

If you run into any issues or come up with any feature suggestions while using this extension, please create an issue [on GitHub](https://github.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/issues/new).
