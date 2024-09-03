import { useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { ValueRendererProps } from '../models';
import type { MultilevelData } from '../../models';
import MultilevelValueEditor from '../../ValueEditor/Editors/MultilevelValueEditor';
import { useBakabaseContext } from '@/components/ContextProvider/BakabaseContextProvider';
import { Chip, Card, CardBody, Button } from '@/components/bakaui';

type MultilevelValueRendererProps = ValueRendererProps<string[][], string[]> & {
  multiple?: boolean;
  getDataSource?: () => Promise<MultilevelData<string>[]>;
};

export default ({ value, editor, variant, getDataSource, multiple, ...props }: MultilevelValueRendererProps) => {
  const { t } = useTranslation();
  const { createPortal } = useBakabaseContext();

  console.log('[MultilevelValueRenderer]', value, variant);

  const showEditor = () => {
    createPortal(MultilevelValueEditor<string>, {
      getDataSource: getDataSource,
      onValueChange: editor?.onValueChange,
      multiple,
      value: editor?.value,
    });
  };

  if (variant == 'light') {
    let label = value?.map(v => v.join('/')).join(';');
    if ((label == undefined || label.length == 0) && editor) {
      label = t('Click to set');
    }
    return (
      <Button
        variant={'light'}
        size={'sm'}
        radius={'sm'}
        onClick={editor ? showEditor : undefined}
      >{label}</Button>
    );
  } else {
    return (
      <Card onClick={editor ? showEditor : undefined}>
        <CardBody className={'flex flex-wrap gap-1'}>
          {value?.map(v => {
            const str = v.join('/');
            return (
              <Chip
                size={'sm'}
                radius={'sm'}
              >
                {str}
              </Chip>
            );
          })}
        </CardBody>
      </Card>
    );
  }
};