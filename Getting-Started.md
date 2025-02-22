# Getting Started

## Video

If you prefer a video tutorial, [click here](https://www.youtube.com/watch?v=guJgtQHQwPo) to watch CodingWithGreg's YouTube video.

## Installation

1. Download from the [Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=TheronWang.TailwindCSSIntellisense) or directly from the IDE.

	- If you are downloading from the IDE, open Extensions > Manage Extensions and search up 'Tailwind'.

	![IDE Menu](art/getting-started/ide-install.png)

	**IMPORTANT**: For Tailwind v3 projects, `npm` and `node` are required for building and configuration file parsing--follow the [official guide](https://docs.npmjs.com/downloading-and-installing-node-js-and-npm) if you do not already have it installed.

## Existing Projects

2. The extension will automatically try to find a css file with `@import "tailwindcss"` or a JavaScript configuration file named `tailwind.config.{js,cjs,mjs,ts,cts,mts}` in your solution. If not found, however, you can manually set it by **right-clicking your file and selecting 'Set as Tailwind CSS configuration file'**.

	**Extension features will only be enabled if a configuration file is found.**

	![Customizability Build 1](art/Customizability-Build-1.png)

## New Project

2a.	Once you have created a project, **right click on the project node and click 'Set up and install Tailwind CSS'**:

![Set up TailwindCSS](art/NPM-Shortcuts-1.png)

This will install `tailwindcss` `@tailwindcss/cli` (if needed) and create a new `tailwind.css` file with content `@import "tailwindcss";`.

**Using the standalone Tailwind CSS CLI:**
- If you have the Tailwind CSS CLI installed, you can click 'Set up Tailwind CSS (use CLI)' instead once you have specified the CLI path in Tools > Options > Tailwind CSS IntelliSense > Tailwind CLI path.
- Following builds in the project will use the Tailwind CLI instead of `npx @tailwindcss/cli`
- If you want to switch between `npx @tailwindcss/cli` and the standalone CLI, adjust the `UseCli` property in `tailwind.extension.json`

2b. To configure Tailwind CSS, follow the [official documentation](https://tailwindcss.com/docs/installation/tailwind-cli).

----
#### V3

- Include the following in your input css file:
```css
@tailwind base;
@tailwind components;
@tailwind utilities;
```
- Ensure your configuration file has a valid `content` value (e.g. `["./**/*.{html,cshtml,razor,js}"]`)

----
#### V4

- Include the following in your input css file:
```css
@import "tailwindcss";
```

## Setting Up the Extension

3. _Setting an input file is only necessary for projects using Tailwind v3._ Before you are ready to build, **set your input CSS file**. Your output file will automatically be generated as `{input file name}.output.css`. If you want to specify a certain file, you can right click and click 'Link as Tailwind CSS output file' and select the corresponding input file. **Input files must already be defined prior to selecting an output file.**

![Input and output CSS files](art/Customizability-Build-2.png)

4. IntelliSense will display once you type in any HTML or CSS files, including `.html`, `.css`, `.cshtml`, `.razor` files and more, **as long as there is either a `tailwind.config.{js,cjs,mjs,ts,cts,mts}` file or another configuration file specified**.

![IntelliSense](art/IntelliSense-Demo-1.gif)

5. **Your Tailwind file will automatically build on project build**, but you can also manually start the build process under Build > Build Tailwind CSS.

![Build process](art/Build-Demo-1.png)

6. **Settings for this extension can be updated in Tools > Options > Tailwind CSS IntelliSense.**

## Customizing Your `tailwind.extension.json` File

- `ConfigurationFiles`: The relative path to your configuration files (i.e. `tailwind.config.js`). This will be parsed for IntelliSense purposes.
	- `IsDefault`: The first configuration file with this set to `true` will be configured as the default. If no default is defined, the first configuration file (regardless of its value) will be set as default.
	- `ApplicableLocations`: The base **directories** in which the configuration file takes effect. This will be used for both IntelliSense and builds, so ensure that your input css file is located in the correct directory.
- `BuildFiles`: A list of files (relative paths) to build when the project is built. This is useful if you have multiple CSS files that need to be built.
    - `Input`: The input css file. Each input file may only have one output file.
	- `Output`: The output css file.
- `PackageConfigurationFile`: The relative path to your `package.json` file. This is used to determine if a custom build should be used, if you defined an npm script to run in the extension's settings.
- `UseCli`: Whether to use the Tailwind CLI or `npx tailwindcss` for building. If you have the Tailwind CLI installed and you have defined its location in the extension's settings, you can set this to `true` to use it instead.
- `CustomRegexes`: Extra regexes to use for completion contexts, sorting, linting, etc., allowing separate options for Razor, HTML, and JS contexts.
	- `Override`: Whether to override the default regexes. If set to `true`, the extension's default regexes will be ignored.
	- `Values`: A list of regexes, in order of priority, to use for each context.
	- **Important notes**:
		- Regexes may use any valid C# engine regex syntax.
		- Your class content must be located in the `content` capture group. For example, the regex `class="(?<content>.*)"` successfully captures `content`.
		- Custom regexes take precedence over default functionality; that is, if a context is matched in both your custom regex and the original regex, the custom regex will be used.
		- It's recommended to thoroughly test your regexes before adding it into your options. Unintended behavior may occur if the regex is too general or if it overlaps with existing functionality.

Sample (v4) file:
```json
{
  	"$schema": "https://raw.githubusercontent.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/refs/heads/main/tailwind.extension.schema.json",
	"ConfigurationFiles": [
		{
		    "Path": "..\\Client\\tailwind.css",
		    "IsDefault": false,
		    "ApplicableLocations": [
			    "..\\Client\\Pages\\"
		    ]
		},
		{
		    "Path": "tailwind.css",
		    "IsDefault": true,
		    "ApplicableLocations": []
		}
	],
	"BuildFiles": [
		{
			"Input": "..\\Client\\tailwind.css",
			"Output": "..\\Client\\tailwind.output.css"
		},
		{
			"Input": "tailwind.css",
			"Output": "tailwind.min.css"
		}
	],
	"PackageConfigurationFile": "package.json",
	"UseCli": false,
	"CustomRegexes": {
		"Razor": {
			"Override": false,
			"Values": [
				"your regex"
			]
		},
		"HTML": {
			"Override": false,
			"Values": []
		},
		"JavaScript": {
			"Override": true,
			"Values": [
				"your regex"
			]
		}
	}
}
```

Sample (v3) file:
```json
{
  	"$schema": "https://raw.githubusercontent.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/refs/heads/main/tailwind.extension.schema.json",
	"ConfigurationFiles": [
		{
		    "Path": "..\\Client\\tailwind.config.js",
		    "IsDefault": false,
		    "ApplicableLocations": [
			    "..\\Client\\Pages\\"
		    ]
		},
		{
		    "Path": "tailwind.config.js",
		    "IsDefault": true,
		    "ApplicableLocations": []
		}
	],
	"BuildFiles": [
		{
			"Input": "site.css",
			"Output": "wwwroot\\css\\site.output.css"
		},
		{
			"Input": "Components\\App.razor.tailwind.css",
			"Output": "Components\\App.razor.css"
		}
	],
	"PackageConfigurationFile": "package.json",
	"UseCli": false,
	"CustomRegexes": {
		"Razor": {
			"Override": false,
			"Values": [
				"your regex"
			]
		},
		"HTML": {
			"Override": false,
			"Values": []
		},
		"JavaScript": {
			"Override": true,
			"Values": [
				"your regex"
			]
		}
	}
}
```
