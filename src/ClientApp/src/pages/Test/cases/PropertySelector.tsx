import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import PropertySelector from '@/components/PropertySelector';
import { Button } from '@/components/bakaui';
import type { ResourceSearchFilter } from '@/pages/Resource/components/FilterPanel/FilterGroupsPanel/models';
import { PropertyPool } from '@/sdk/constants';

export default () => {
  const { t } = useTranslation();
  const [filter, setFilter] = useState<ResourceSearchFilter>({});

  return (
    <Button
      type={'primary'}
      text
      onClick={() => {
        PropertySelector.show({
          selection: { [filter.propertyPool == PropertyPool.Custom ? 'reservedPropertyIds' : 'customPropertyIds']: filter.propertyId == undefined ? undefined : [filter.propertyId] },
          onSubmit: async (selectedProperties) => {
            const property = (selectedProperties.reservedProperties?.[0] ?? selectedProperties.customProperties?.[0])!;
            const cp = property as ICustomProperty;
            setFilter({
              ...filter,
              propertyId: property.id,
              propertyName: property.name,
              propertyPool: cp == undefined,
            });
          },
          multiple: false,
          pool: 'all',
        });
      }}
      size={'small'}
    >
      {filter.propertyId ? filter.propertyName : t('Property')}
    </Button>
  );
};
