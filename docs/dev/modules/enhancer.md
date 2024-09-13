# Enhancer development <!-- {docsify-ignore} -->

## Overview

| Enhancer | Description | Data Source | Status | 
| ------------- | ------------- | ------------- | ------------- |
| Bakabase | / | Filename, Files inside(cover) | finished |
| ExHentai | / | https://exhentai.org | finished |
| Bangumi | / | https://bgm.tv | finished |
| DLsite | / | http://www.dlsite.com | finished |
| Regex | / | Filename | finished |
| NFO | popular NFO formats | *.nfo | not started |

## Concepts

TBD

## How to create an enhancer

> *I'm working for detailed docs, you can take a look into other implementations for now.*

1. Add a folder in `src/modules/Bakabase.Modules.Enhancer/Components/Enhancers/` with the name of the enhancer.
2. Pick a name for the enhancer, such as `{Name}`.
3. Add `{Name}Enhancer.cs`, `{Name}EnhancerContext.cs` and `{Name}EnhancerTarget.cs` files in the folder.
4. If the enhancer needs apply http requests to external data source, you should also implement `{Name}Client` for it in `src/Bakabase.Modules.ThirdParty/ThirdParties`.
5. Add a new field to `EnhancerId`.
6. Add a new field to `PropertyValueScope`.
7. Add some resources for localization.