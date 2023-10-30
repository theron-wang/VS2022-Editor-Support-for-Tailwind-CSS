# Getting Started

## Installation

1. Download from the [Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=TheronWang.TailwindCSSIntellisense) or directly from the IDE.

- If you are downloading from the IDE, open Extensions > Manage Extensions and search up 'Tailwind'.

![IDE Menu](art/getting-started/ide-install.png)

**IMPORTANT**: This extension uses `npm` and `node` for various purposes--follow the [official guide](https://docs.npmjs.com/downloading-and-installing-node-js-and-npm) if you do not already have it installed.

## Opening a New Project

2. Once you have created a project, right click on the project node and click 'Set up Tailwind CSS':

![Set up TailwindCSS](art/NPM-Shortcuts-1.png)

This will import the Tailwind CSS node modules and configure your `tailwind.config.js`.

3. To configure Tailwind CSS, follow the [official documentation](https://tailwindcss.com/docs/installation) (specifically steps 2, 3, and 5).

- Make sure you include `@tailwind base; @tailwind components; @tailwind utilities;` in your css file or your file will not build.
- Also, make sure your `tailwind.config.js` has a valid `content` value, such as `["./**/*.{html,cshtml,razor,js}"]`.

4. Before you are ready to build, set your input CSS file. Your output file will automatically be generated as `{input file name}.output.css`. If you want to specify a certain file, you can right click and click 'Set as Tailwind CSS output file'.

![Input and output CSS files](art/Customizability-Build-2.png)

5. Type in any HTML or CSS files, including `.html`, `.css`, `.cshtml`, `.razor` among other files, and IntelliSense will show up.

![IntelliSense](art/IntelliSense-Demo-1.gif)

6. To build your file, either manually start the build process under Build > Start Tailwind CSS build process.

![Build process](art/Build-Demo-1.png)

Settings for this extension can be updated in Tools > Options > TailwindCSS IntelliSense.