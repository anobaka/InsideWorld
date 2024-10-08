import dayjs from 'dayjs';
import React from 'react';
import { useTranslation } from 'react-i18next';
import type { Resource } from '@/core/models/Resource';

type Props = {
  resource: Resource;
};

const dateTimes = [
  {
    key: 'fileCreateDt',
    label: 'File Add Date',
  },
  {
    key: 'fileModifyDt',
    label: 'File Modify Date',
  },
  {
    key: 'createDt',
    label: 'Resource Create Date',
  },
  {
    key: 'updateDt',
    label: 'Resource Update Date',
  },
];

export default ({ resource }: Props) => {
  const { t } = useTranslation();
  return (
    <div className={'grid justify-evenly gap-y-1'} style={{ gridTemplateColumns: 'repeat(2, auto)' }}>
      {dateTimes.map((dateTime, i) => {
        const label = t(dateTime.label);
          const value = dayjs(resource[dateTime.key]).format('YYYY-MM-DD HH:mm:ss');
          return (
            <div key={i} className={'flex flex-col'}>
              <div className={'text-xs opacity-60'}>{label}</div>
              <div>{value}</div>
            </div>
          );
        })}
    </div>
  );
};
