# Changelog

## 1.2.4 (March 24th, 2024)

### Additions

- Support for arm64 ([#37](https://github.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/issues/37))
- Support for standalone Tailwind CSS CLI

## 1.2.3 (March 7th, 2024)

### Fixes

- Certain `theme.colors` attributes would not appear in IntelliSense

### Additions

- Adds support for `.ts` files as configuration files

## 1.2.2 (February 7th, 2024)

### Fixes

- Fixed a `System.IO.FileNotFoundException` which could occur in rare cases ([#31](https://github.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/issues/31))
- Linting, completions, and quick info now work in Razor files when `@` is used within class attributes

## 1.2.1 (January 20th, 2024)

### Fixes

- Fixed a bug where the `tailwind.extension.json` file preferences would be ignored ([#32](https://github.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/issues/32))

## 1.2.0 (January 1st, 2024)

### Fixes

- Fixed an exception which could occur when typing in `css` files

### Additions

- Linting support in all related files ([#28](https://github.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/issues/28))
- Error list support
- Linter configuration settings

## 1.1.9.3 (December 19th, 2023)

### Fixes

- Fixed an exception which could occur when Set up Tailwind CSS ([#31](https://github.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/issues/31))
- `theme` values would sometimes be ignored in configuration files
- Fixed a bug where certain classes would still appear despite being overriden

### Enhancements

- Added an option in settings to minify builds by default
- Updated IntelliSense to support Tailwind v3.4

## 1.1.9.2 (December 5th, 2023)

### Fixes

- Fixed errors which would occur when using ES modules ([#24](https://github.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/issues/24))

## 1.1.9.1 (December 4th, 2023)

### Fixes

- Fixed an exception which could occur when referencing plugins using `import` ([#24](https://github.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/issues/24))

## 1.1.9 (December 3rd, 2023)

### Additions

- IntelliSense support for plugins ([#24](https://github.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/issues/24))

### Fixes

- Default IntelliSense may still show up in some cases ([#30](https://github.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/issues/30))

## 1.1.8.8 (November 24th, 2023)

### Additions

- Added IntelliSense support for `.tcss` files ([#27](https://github.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/issues/27))

### Enhancements

- Using ctrl + space in the middle of a class will now display relevant completions for the entire class

### Fixes

- Fixed an exception which would occur when using `plugins` ([#26](https://github.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/issues/26))

## 1.1.8.7 (November 5th, 2023)

### Additions

- Ability to create minified output css files
- New build option: build once when entire project is built
- `tailwind.config.js` files will automatically be found when opening an existing project ([#22](https://github.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/issues/22))

### Enhancements

- Renamed certain menu items to better reflect their purpose ([#23](https://github.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/issues/23))
- Performance improvements when typing `/` for transparency variants

## 1.1.8.6 (October 29th, 2023)

### Additions

- Support for `({ theme })` in configuration files

### Fixes

- Minor performance fixes for transparency IntelliSense

## 1.1.8.5 (September 20th, 2023)

### Additions

- Support for `CssClass` in ASP.NET Web Forms controls ([#20](https://github.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/issues/20))
- Full description support for `fontSize`

### Fixes

- Fixed a bug where custom descriptions would sometimes fail to appear

## 1.1.8.4 (September 8th, 2023)

### Additions

- Support for `prefix` values ([#19](https://github.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/issues/19))
- Added description support for `fontFamily` and `dropShadow` (`fontSize` not supported)

## 1.1.8.3 (August 30th, 2023)

### Fixes

- Added extra guard clauses for QuickInfo tooltips ([#17](https://github.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/issues/17))
- Moved entire package initialization to be executed on a background thread (performance issues cited in [#17](https://github.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/issues/17))

## 1.1.8.2 (August 29th, 2023)

### Fixes

- Fixed QuickInfo tooltips showing more than 1 description ([#17](https://github.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/issues/17))
- QuickInfo tooltips would not show up with `whitespace-...` variants
- Fixed an issue where backspacing from the end of a class would not show other completions

### Enhancements

- Added `px` values for non-spacing `rem` values

## 1.1.8.1 (August 25th, 2023)

### Enhancements

- IntelliSense now scrolls correctly when filtering (i.e. typing `border-b` will now prioritize `border-b` instead of `border-blue-*`)

### Fixes

- IntelliSense did not show any modifiers (i.e. `active:`, `focus:`) in certain cases
- Fixed an exception which would occur when hovering over Tailwind classes before the project was loaded
- Build process would build to default file when adding a new project (related to [#14](https://github.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/issues/14))

## 1.1.8 (August 23rd, 2023)

### Additions

- Re-written configuration file parser to support referencing other files (i.e. using `require('tailwindcss/defaultTheme')` to reference the default theme)
- Added hover tooltips for Tailwind classes

### Fixes

- Error loading `tailwind.config.js` when certain properties are used ([#16](https://github.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/issues/16))

### Enhancements

- Changed Tailwind update check to be asynchronous to prevent UI locking

## 1.1.7.8 (August 21st, 2023)

### Fixes

- Classes would not show up consistently with IntelliSense ([#14](https://github.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/issues/14))
- Spacing mapper now works when local culture uses commas instead of dots ([#15](https://github.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/issues/15))
- Fixed an issue where `tailwind.extension.json` could not be found when multiple projects are loaded

## 1.1.7.7 (August 19th, 2023)

### Fixes

- Added null checks where errors could occur ([#14](https://github.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/issues/14))

## 1.1.7.6 (August 18th, 2023)

### Fixes

- Exceptions fixed when IntelliSense triggered with certain configuration files ([#8](https://github.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/issues/8))
- Completions no longer show up in a non-Tailwind project

### Enhancements

- IntelliSense now shows up with Blazor components using a `Class=""` parameter instead of `class=""` ([#12](https://github.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/issues/12))
- Improved IntelliSense filtering: for example, typing `hue-60` will now display `backdrop-hue-rotate-60`

## 1.1.7.5 (August 17th, 2023)

### Fixes

- Null reference exception would occur when IntelliSense triggered ([#8](https://github.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/issues/8))

## 1.1.7.4 (August 16th, 2023)

### Enhancements

- Build settings are now updated when `tailwind.extension.json` is directly modified ([#11](https://github.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/issues/11))

## 1.1.7.3 (August 14th, 2023)

### Fixes

- Fixed incorrect descriptions for negative spacing values
- CSS output file becomes null when file does not exist ([#11](https://github.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/issues/11))
- Build menu in an open folder context would not show up (moved toggle to solution explorer folder node)

## 1.1.7.2 (August 10th, 2023)

### Enhancements

- Added `px` values for spacing in completion descriptions ([#9](https://github.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/issues/9))

## 1.1.7.1 (August 9th, 2023)

### Fixes

- Fixed an exception which would occur with specific configuration files ([#8](https://github.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/issues/8))
- Fixed a bug where custom colors would not show up in IntelliSense

## 1.1.7 (August 8th, 2023)

### Additions

- CSS descriptions for each class ([#7](https://github.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/issues/7))
- Added support for missing configuration values

### Fixes

- rgb() values now supported within `theme.colors` and `theme.extend.colors`
- Spacing values (i.e. padding, margin) now ordered correctly

## 1.1.6 (August 3rd, 2023)

### Additions

- Support for Web Forms files (.aspx, .ascx, .master) ([#6](https://github.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/issues/6))

### Fixes

- `package.json` script would sometimes not update correctly

## 1.1.5 (August 2nd, 2023)

### Enhancements

- IntelliSense shows up if you backspace or type at the end of a class
- Added colons to the end of each modifier for clarity
- Removed modifiers showing up before each completion

### Additions

- Support for `max-{breakpoint}:`
- Support for group and all group modifiers

## 1.1.4.2 (July 31st, 2023)

### Fixes

- Fixed a bug where configuration values would not update
- Fixed an error which would occur when backspacing in empty CSS files

## 1.1.4.1 (July 31st, 2023)

### Fixes

- Fixed a bug where completions would not show up

## 1.1.4 (July 31st, 2023)

### Additions

- Support for most configuration specifications
- Added ability to override the default build process or supplement it with a package.json script ([#5](https://github.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/issues/5))

### Enhancements

- Added arbitrary completions for all those that support it

### Fixes

- Tailwind completions would persist when switching to another project, even if it does not have Tailwind enabled

## 1.1.3 (July 28th, 2023)

### Fixes

- Minor bug fix: wrong method being unsubscribed in `TailwindBuildProcess` ([#3](https://github.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/pull/3))
- Fixed an issue where the status bar loading animation would continue to play

## 1.1.2 (July 26th, 2023)

### Fixes

- Added missing classes

## 1.1.1 (July 26th, 2023)

### Enhancements

- Better IntelliSense color sorting ([#1](https://github.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/issues/1))
- Contains IntelliSense filtering instead of starts with ([#1](https://github.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/issues/1))
- Status bar and output pane now shows more information on build (time taken, time when built)

### Fixes

- Fixed a bug where CSS completion would not commit

## 1.1.0 (July 25th, 2023)

### Additions

- IntelliSense transparency support for colors (i.e. `bg-green-700/50`)
- New build option - On Save: runs an `npx` build command on each file save; can be used if default build option is not reliable

### Fixes

- Fixed more classes which were not previously showing up in IntelliSense

## 1.0.3 (July 24th, 2023)

### Fixes

- Fixed certain incorrect/missing classes in IntelliSense

## 1.0.2 (July 21st, 2023)

### Fixes

- Build would start in projects with no Tailwind configuration
- Fixed an issue where the `npx tailwind --watch` process would fail to terminate

## 1.0.1 (July 20th, 2023)

### Fixes

- Fixed an error which would sometimes occur when switching between projects
