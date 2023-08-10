# Changelog

## 1.1.7.2 (August 10th, 2023)

## Enhancements

- Added `px` values for spacing in completion descriptions ([#9](https://github.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/issues/))

## 1.1.7.1 (August 9th, 2023)

## Fixes

- Fixed an exception which would occur with specific configuration files
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

- Support for max-{breakpoint}:
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
