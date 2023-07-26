# Tailwind CSS VS2022 Editor Support 

![Visual Studio Marketplace Downloads](https://img.shields.io/visual-studio-marketplace/d/TheronWang.TailwindCSSIntellisense)
![Visual Studio Marketplace Version (including pre-releases)](https://img.shields.io/visual-studio-marketplace/v/TheronWang.TailwindCSSIntellisense)

Editor features such as IntelliSense, build integration, among others for Tailwind CSS to enhance the development experience in Visual Studio 2022.

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

Customization support for `theme.colors`, `theme.screens`, and `theme.spacing` as well as their `theme.extension` counterparts:

![Custom Configuration File](https://raw.githubusercontent.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/main/art/IntelliSense-Demo-2-Configuration.png)

![Intellisense Demo 2](https://raw.githubusercontent.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/main/art/IntelliSense-Demo-3.png)

Other attributes, such as `borderRadius` and `fontFamily`, are not yet supported.

### Build Integration

In a project, simply run the project to build Tailwind CSS or access it via the Build menu:

![Build Demo 1](https://raw.githubusercontent.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/main/art/Build-Demo-1.png)

Or in an open folder, go directly to the Build menu to start the build process:

![Open Folder Build Demo](https://raw.githubusercontent.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/main/art/Build-Demo-3.png)

After the process has started, the process will continue to build and output data to the Build window pane:

![Build Demo 2](https://raw.githubusercontent.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/main/art/Build-Demo-2.png)

### NPM Shortcuts

When getting started in a new project, you can import the necessary modules by right-clicking on the project node:

![Screenshot](https://raw.githubusercontent.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/main/art/NPM-Shortcuts-1.png)

### Customizability

#### Build

If you want to explicitly state the configuration file, you can right click any JavaScript file as follows:

![Customizability Build 1](https://raw.githubusercontent.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/main/art/Customizability-Build-1.png)

Likewise, for the build file and the output file, you can also explicitly state what CSS files you want:

![Customizability Build 2](https://raw.githubusercontent.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/main/art/Customizability-Build-2.png)

If you click on each file again, you will find a remove option to set it back to the default.

Please note that if you click any of these buttons, a tailwind.extension.json file will be created in your project root.

#### Extension Options

By going into Tools > Options > TailwindCSS Intellisense, you can alter the global behavior for the extension:

![Options](https://raw.githubusercontent.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/main/art/Options-Demo.png)

From here, you can choose to do the following:

* Change how Tailwind files are built
* Alter the default build output file name
* Disable/enable automatic Tailwind module updates
* Disable/enable extension features globally
* Change the order in which completions are appended

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
