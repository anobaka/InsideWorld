---
globs:
  - "**/Search/Index/**"
  - "**/Modules.Property/**/Properties/**"
  - "**/IPropertyIndexProvider.cs"
  - "**/SearchIndexTask.cs"
---

# Resource Search Index System

## Overview

The resource search system uses an **inverted index** for fast property-based filtering. When the index is not ready, the system automatically falls back to full-scan search.

## Architecture

```
ResourceSearchIndexService (legacy/)
    ├── ValueIndex: PropertyPool → PropertyId → NormalizedValue → HashSet<ResourceId>
    ├── RangeIndex: PropertyPool → PropertyId → SortedList<IComparable, HashSet<ResourceId>>
    └── ResourceIndexKeys: ResourceId → Set<IndexKey>  (for cleanup)
```

## BTask Integration

- **Task Name**: `SearchIndex`
- **Location**: `SearchIndexTask.cs` (`src/legacy/.../Search/Index/`)
- **Type**: One-time startup task (Interval = null)
- **Progress**: Resets to 0% when rebuilding

## Index Key Generation

Each property type implements `IPropertyIndexProvider` to generate index entries:

```csharp
public interface IPropertyIndexProvider
{
    IEnumerable<PropertyIndexEntry> GenerateIndexEntries(Property property, object? dbValue);
}

public record struct PropertyIndexEntry(string Key, IComparable? RangeValue = null);
```

### Property Type Index Mappings

| PropertyType | DbValueType | Index Strategy | Key Format | RangeValue |
|-------------|-------------|----------------|------------|------------|
| **SingleLineText** | String | Single key | Lowercase text | - |
| **MultilineText** | String | Single key | Lowercase text | - |
| **SingleChoice** | String | Single key | Choice UUID | - |
| **MultipleChoice** | ListString | **Multi-key** | Each choice UUID separately | - |
| **Tags** | ListString | **Multi-key** | Each tag UUID separately | - |
| **Multilevel** | ListString | **Multi-key** | Each node UUID separately | - |
| **Attachment** | ListString | **Multi-key** | Each file path separately | - |
| **Number** | Decimal | Single key + Range | String value | `decimal` |
| **Percentage** | Decimal | Single key + Range | String value | `decimal` |
| **Rating** | Decimal | Single key + Range | String value | `decimal` |
| **Date** | DateTime | Single key + Range | Timestamp string | `DateTime` |
| **DateTime** | DateTime | Single key + Range | Timestamp string | `DateTime` |
| **Time** | Time | Single key + Range | Milliseconds string | `TimeSpan` |
| **Boolean** | Boolean | Single key | `"true"` / `"false"` | - |
| **Link** | Link | Single key | URL string | - |

### Internal Properties

| Property | Index Type | Key Format |
|----------|-----------|------------|
| Filename | ValueIndex | Lowercase filename |
| DirectoryPath | ValueIndex | Lowercase directory path |
| RootPath | ValueIndex | Lowercase full path |
| CreatedAt | RangeIndex | DateTime |
| FileCreatedAt | RangeIndex | DateTime |
| FileModifiedAt | RangeIndex | DateTime |
| PlayedAt | RangeIndex | DateTime |
| MediaLibraryV2Multi | ValueIndex | Each library ID as string |

### Reserved Properties

| Property | Index Type | Key Format |
|----------|-----------|------------|
| Rating | RangeIndex | Decimal value |
| Introduction | ValueIndex | Full text lowercase |
| Cover | ValueIndex | Each cover path separately |

## Key Design Decisions

### 1. Multi-Key for List Types

**Problem**: Joining list values with `|` makes individual item search impossible.

**Solution**: Generate separate index entries for each list item.

```csharp
// Tags example
protected override IEnumerable<PropertyIndexEntry> GenerateIndexEntriesInternal(
    Property property, List<string> dbValue)
{
    foreach (var tagId in dbValue)
    {
        if (!string.IsNullOrEmpty(tagId))
            yield return new PropertyIndexEntry(tagId);
    }
}
```

### 2. Dual Index for Numeric/DateTime Types

Values are indexed in both:
- **ValueIndex**: For equality and contains operations
- **RangeIndex**: For comparison operations (>, <, >=, <=)

### 3. Fallback Search

When `SearchResourceIdsAsync` returns `null`:
1. Index not ready (during rebuild)
2. An incremental update failed and no successful full rebuild has recovered the index
3. No filter specified

The caller (ResourceService) falls back to full-scan search at `ResourceService.cs:188-218`.

## Incremental Updates

Resource changes are batched via a single-reader Channel:

```csharp
// Invalidate (update index)
InvalidateResource(int resourceId)
InvalidateResources(IEnumerable<int> resourceIds)

// Remove from index
RemoveResource(int resourceId)
RemoveResources(IEnumerable<int> resourceIds)
```

Each bulk notification is deduplicated and enqueued in bounded chunks of at most 4096
resource IDs. The reader then coalesces adjacent chunks for up to 500ms or 4096 distinct
resources, whichever comes first. This avoids repeating the full set of cache reads for
every small batch while keeping memory and per-batch work bounded. If both update and remove
notifications for one resource occur in the same barrier-delimited batch, remove wins, which
preserves the previous batching semantics and prevents a stale row from being resurrected.

`WaitForPendingUpdatesAsync` adds a FIFO barrier to the same channel. It completes only
after every update queued before it has been applied; work queued after the barrier does
not delay that wait. Workflows that promise immediately searchable results (for example,
path-mark synchronization) must enqueue all affected resource IDs and await one barrier
before reporting completion.

Full rebuilds and incremental batches share a mutation gate so they cannot write the index
at the same time. Incremental batches are retried up to three times. A permanently failed
batch does not advance the index version, faults subsequent barriers, marks the index
unavailable, and forces searches to use the full-scan fallback until a full rebuild succeeds.

## Key Files

| Purpose | Location |
|---------|----------|
| Index Service | `src/legacy/.../Search/Index/ResourceSearchIndexService.cs` |
| Index Storage | `src/legacy/.../Search/Index/ResourceSearchIndex.cs` |
| Index Key | `src/legacy/.../Search/Index/IndexKey.cs` |
| IPropertyIndexProvider | `src/modules/Bakabase.Modules.Property/Abstractions/Components/IPropertyIndexProvider.cs` |
| Property Descriptors | `src/modules/Bakabase.Modules.Property/Components/Properties/*/` |
| BTask Definition | `src/legacy/.../Search/Index/SearchIndexTask.cs` |

## Adding New Property Types

1. Create descriptor in `Bakabase.Modules.Property/Components/Properties/`
2. Extend `AbstractPropertyDescriptor<TDbValue, TBizValue>`
3. Override `GenerateIndexEntriesInternal` if default behavior is insufficient
4. For list types: Generate separate entries for each item
5. For numeric/datetime: Include `RangeValue` in the entry

```csharp
protected override IEnumerable<PropertyIndexEntry> GenerateIndexEntriesInternal(
    Property property, TDbValue dbValue)
{
    // For list types
    foreach (var item in dbValue)
        yield return new PropertyIndexEntry(item);

    // For numeric types
    yield return new PropertyIndexEntry(dbValue.ToString(), dbValue);
}
```
