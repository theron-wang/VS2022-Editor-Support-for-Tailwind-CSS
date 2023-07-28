# Changelog

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
