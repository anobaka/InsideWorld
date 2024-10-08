import { SearchableReservedProperty, SearchOperation } from '@/sdk/constants';

export const SearchableReservedPropertySearchOperationsMap: { [key in SearchableReservedProperty]?: SearchOperation[] } = {
  [SearchableReservedProperty.FileName]: [
    SearchOperation.Equals,
    SearchOperation.NotEquals,
    SearchOperation.Contains,
    SearchOperation.NotContains,
    SearchOperation.StartsWith,
    SearchOperation.EndsWith,
    SearchOperation.Matches,
    SearchOperation.NotMatches,
  ],
  [SearchableReservedProperty.DirectoryPath]: [
    SearchOperation.Contains,
    SearchOperation.NotContains,
    SearchOperation.StartsWith,
    SearchOperation.EndsWith,
    SearchOperation.Matches,
    SearchOperation.NotMatches,
  ],
  [SearchableReservedProperty.CreatedAt]: [
    SearchOperation.Equals,
    SearchOperation.NotEquals,
    SearchOperation.GreaterThan,
    SearchOperation.LessThan,
    SearchOperation.GreaterThanOrEquals,
    SearchOperation.LessThanOrEquals,
  ],
  [SearchableReservedProperty.FileCreatedAt]: [
    SearchOperation.Equals,
    SearchOperation.NotEquals,
    SearchOperation.GreaterThan,
    SearchOperation.LessThan,
    SearchOperation.GreaterThanOrEquals,
    SearchOperation.LessThanOrEquals,
  ],
  [SearchableReservedProperty.FileModifiedAt]: [
    SearchOperation.Equals,
    SearchOperation.NotEquals,
    SearchOperation.GreaterThan,
    SearchOperation.LessThan,
    SearchOperation.GreaterThanOrEquals,
    SearchOperation.LessThanOrEquals,
  ],
  [SearchableReservedProperty.MediaLibrary]: [
    SearchOperation.In,
    SearchOperation.NotIn,
  ],
  [SearchableReservedProperty.Rating]: [
    SearchOperation.Equals,
    SearchOperation.NotEquals,
    SearchOperation.GreaterThan,
    SearchOperation.LessThan,
    SearchOperation.GreaterThanOrEquals,
    SearchOperation.LessThanOrEquals,
  ],
  [SearchableReservedProperty.Introduction]: [
    SearchOperation.Contains,
    SearchOperation.NotContains,
    SearchOperation.IsNotNull,
    SearchOperation.IsNull,
  ],
};

