# Changelog

## 1.1.0 (July 25th, 2023)

### New Features

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
