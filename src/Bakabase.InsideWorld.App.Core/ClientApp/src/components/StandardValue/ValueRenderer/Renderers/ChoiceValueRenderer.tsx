import { useRef, useState } from 'react';
import type { ValueRendererProps } from '../models';
import type { EditableValueProps } from '../../models';
import MultilevelValueEditor from '../../ValueEditor/Editors/MultilevelValueEditor';
import ChoiceValueEditor from '../../ValueEditor/Editors/ChoiceValueEditor';
import { useBakabaseContext } from '@/components/ContextProvider/BakabaseContextProvider';
import { Chip, Card, CardBody } from '@/components/bakaui';

type Data = {label: string; value: string};

type ListStringValueRendererProps = ValueRendererProps<string[]> & EditableValueProps<string[]> & {
  multiple?: boolean;
  getDataSource?: () => Promise<Data[]>;
};

export default ({ value, onValueChange, editable, variant, getDataSource, multiple, ...props }: ListStringValueRendererProps) => {
  const { createPortal } = useBakabaseContext();

  const dataSourceRef = useRef<Data[]>([]);

  const showEditor = () => {
    createPortal(ChoiceValueEditor, {
      getDataSource: async () => dataSourceRef.current,
      onChange: v => {
        if (v) {
          onValueChange?.(v);
        }
      },
      multiple: multiple ?? false,
    });
  };

  if (variant == 'light') {
    return (
      <span onClick={editable ? showEditor : undefined}>{value?.join(', ')}</span>
    );
  } else {
    return (
      <Card onClick={editable ? showEditor : undefined}>
        <CardBody className={'flex flex-wrap gap-1'}>
          {value?.map(d => {
            return (
              <Chip
                size={'sm'}
                radius={'sm'}
              >
                {d}
              </Chip>
            );
          })}
        </CardBody>
      </Card>
    );
  }
};
