import { useTranslation } from 'react-i18next';
import React, { useEffect, useRef } from 'react';
import { useUpdateEffect } from 'react-use';
import type { Root } from 'react-dom/client';
import { createRoot } from 'react-dom/client';
import type { DataPool, IGroup } from '../models';
import { GroupCombinator } from '../models';
import styles from './index.module.scss';
import Filter from './Filter';
import ClickableIcon from '@/components/ClickableIcon';
import CustomIcon from '@/components/CustomIcon';
import type { IProperty } from '@/components/Property/models';
import { Button, Chip, Dropdown, Popover } from '@/components/bakaui';

interface IProps {
  group: IGroup;
  onRemove?: () => void;
  onChange?: (group: IGroup) => void;
  isRoot?: boolean;
  portalContainer?: any;
  propertyMap: Record<number, IProperty>;
  dataPool?: DataPool;
}

const FilterGroup = ({
                       group: propsGroup,
                       onRemove,
                       onChange,
                       isRoot = false,
                       portalContainer,
                       propertyMap,
                       dataPool,
                     }: IProps) => {
  const { t } = useTranslation();
  const [group, setGroup] = React.useState<IGroup>(propsGroup);
  const groupRef = useRef(group);

  useEffect(() => {
    let root: Root;
    if (portalContainer) {
      root = createRoot(portalContainer);
      root.render(renderAddHandler());
    }

    return () => {
      root?.unmount();
    };
  }, []);

  useUpdateEffect(() => {
    groupRef.current = group;
    onChange?.(group);
  }, [group]);

  useUpdateEffect(() => {
    setGroup(propsGroup);
    console.log(propsGroup, 123);
  }, [propsGroup]);

  const {
    filters,
    groups,
    combinator,
  } = group;

  const conditionElements: any[] = (filters || []).map((f, i) => (
    <Filter
      dataPool={dataPool}
      propertyMap={propertyMap}
      key={`f-${i}`}
      filter={f}
      onRemove={() => {
        setGroup(
          {
            ...group,
            filters: (group.filters || []).filter(fil => fil !== f),
          },
        );
      }}
      onChange={tf => {
        setGroup(
          {
            ...group,
            filters: (group.filters || []).map(fil => (fil === f ? tf : fil)),
          },
        );
      }}
    />
  )).concat((groups || []).map((g, i) => (
    <FilterGroup
      dataPool={dataPool}
      propertyMap={propertyMap}
      key={`g-${i}`}
      group={g}
      onRemove={() => {
        setGroup({
          ...group,
          groups: (group.groups || []).filter(gr => gr !== g),
        });
      }}
      onChange={tg => {
        setGroup({
          ...group,
          groups: (group.groups || []).map(gr => (gr === g ? tg : gr)),
        });
      }}
    />
  )));

  const allElements = conditionElements.reduce((acc, el, i) => {
    acc.push(el);
    if (i < conditionElements.length - 1) {
      acc.push(
        <Button
          key={`c-${i}`}
          className={'min-w-fit pl-2 pr-2'}
          color={'default'}
          variant={'light'}
          size={'sm'}
          onClick={() => {
            setGroup({
              ...group,
              combinator: GroupCombinator.And == group.combinator ? GroupCombinator.Or : GroupCombinator.And,
            });
          }}
        >
          {t(`Combinator.${GroupCombinator[combinator]}`)}
        </Button>,
      );
    }
    return acc;
  }, []);

  const renderAddHandler = () => {
    return (
      <Popover
        showArrow
        trigger={(
          <Button
            size={'sm'}
            isIconOnly
          >
            <ClickableIcon
              colorType={'normal'}
              type={'add-filter'}
              className={'text-xl'}
            />
          </Button>
        )}
        placement={'bottom'}
      >
        <div className={'flex items-center gap-2'}>
          <Button
            size={'sm'}
            onClick={() => {
              setGroup({
                ...groupRef.current,
                filters: [
                  ...(groupRef.current.filters || []),
                  {},
                ],
              });
            }}
          >
            <div className={styles.text}>
              <CustomIcon
                className={'text-small'}
                type={'filter-records'}
              />
              {t('Filter')}
            </div>
          </Button>
          <Button
            size={'sm'}
            onClick={() => {
              setGroup({
                ...groupRef.current,
                groups: [
                  ...(groupRef.current.groups || []),
                  { combinator: GroupCombinator.And },
                ],
              });
            }}
          >
            <div className={styles.text}>
              <CustomIcon
                className={'text-small'}
                type={'unorderedlist'}
              />
              {t('Filter group')}
            </div>
          </Button>
        </div>
      </Popover>
    );
  };

  return (
    <div className={`${styles.filterGroup} p-1 ${isRoot ? styles.root : ''} ${styles.removable}`}>
      <ClickableIcon
        colorType={'danger'}
        className={styles.remove}
        type={'delete'}
        size={'small'}
        onClick={() => {
          onRemove?.();
        }}
      />
      {allElements}
      {!portalContainer && (
        renderAddHandler()
      )}
    </div>
  );
};

export default FilterGroup;