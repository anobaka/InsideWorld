import React, { useCallback, useState } from 'react';
import { Balloon, Button, Dialog, Input, Message, Select, Tag } from '@alifd/next';
import { useTranslation } from 'react-i18next';
import SimpleLabel from '@/components/SimpleLabel';
import './index.scss';
import ClickableIcon from '@/components/ClickableIcon';

enum VariableSource {
  None = 1,
  FileName,
  FileNameWithoutExtension,
  FullPath,
  DirectoryName,
  Name,
}

export interface IVariable {
  name?: string;
  key: string;
  source: VariableSource;
  find?: string;
  value: string;
}

interface IEditingVariable {
  name?: string;
  key?: string;
  source?: VariableSource;
  find?: string;
  value?: string;
}

interface IProps {
  variables?: IVariable[];
  onChange?: (variables: IVariable[]) => void;
}

const variableSources = Object.keys(VariableSource).filter(a => Number.isNaN(parseInt(a, 10)));

export default ({ variables: propsVariable, onChange }: IProps) => {
  const { t } = useTranslation();

  const [variables, setVariables] = useState<IVariable[]>(propsVariable || []);
  const [editingVariable, setEditingVariable] = useState<IEditingVariable>();

  const closeDialog = useCallback(() => {
    setEditingVariable(undefined);
  }, []);

  // console.log(variables);

  return (
    <div className={'bulk-modification-variables'}>
      <Dialog
        visible={!!editingVariable}
        title={t('Setting variable')}
        width={'auto'}
        closeMode={['close', 'esc', 'mask']}
        onClose={closeDialog}
        onCancel={closeDialog}
        className={'bulk-modification-variable-dialog'}
        onOk={() => {
          console.log(editingVariable);
          if (!editingVariable || editingVariable.key == undefined || editingVariable.key.length == 0 ||
            editingVariable.value == undefined || editingVariable.value.length == 0 ||
            variables.some(v => v.key === editingVariable.key) || variableSources.includes(editingVariable.key) ||
            editingVariable.source == undefined || !variableSources.includes(VariableSource[editingVariable?.source])
          ) {
            return Message.error(t('Invalid data'));
          }
          variables.push(editingVariable as IVariable);
          setVariables([...variables]);
          closeDialog();
        }}
      >
        <div className="variable-form">
          <div className="label">
            {t('Source')}
          </div>
          <div className="value">
            <Select
              dataSource={variableSources.map(s => ({ label: t(s), value: VariableSource[s] }))}
              value={editingVariable?.source}
              onChange={v => {
                setEditingVariable({
                  ...editingVariable,
                  source: v,
                });
            }}
              style={{ width: '100%' }}
            />
          </div>
          <div className="label">
            {t('Name')}
          </div>
          <div className="value">
            <Input
              value={editingVariable?.name}
              onChange={v => {
                setEditingVariable({
                  ...editingVariable,
                  name: v,
                });
              }}
            />
          </div>
          <div className="label">
            {t('Key')}
          </div>
          <div className="value">
            <Input
              state={editingVariable?.key == undefined || variables.some(v => v != editingVariable && v.key === editingVariable?.key) || variableSources.includes(editingVariable?.key) ? 'error' : undefined}
              value={editingVariable?.key}
              onChange={v => {
                setEditingVariable({
                  ...editingVariable,
                  key: v,
                });
              }}
            />
          </div>
          <div className="label">
            {t('Find')}
          </div>
          <div className="value">
            <Input
              value={editingVariable?.find}
              onChange={v => {
                setEditingVariable({
                  ...editingVariable,
                  find: v,
                });
              }}
            />
          </div>
          <div className="label">
            {t('Value')}
          </div>
          <div className="value">
            <Input
              value={editingVariable?.value}
              state={editingVariable?.value == undefined || editingVariable.value.length == 0 ? 'error' : undefined}
              onChange={v => {
                setEditingVariable({
                  ...editingVariable,
                  value: v,
                });
              }}
            />
          </div>
        </div>
      </Dialog>
      <div className={'values'}>
        {variableSources.filter(key => VariableSource[key] != VariableSource.None).map((key: any) => {
          return (
            <div className={'value'}>
              <SimpleLabel
                status={'default'}
                key={key}
              >
                {key}
              </SimpleLabel>
              {t(key)}
            </div>
          );
        })}
        {variables.map((v, i) => {
          return (
            <Balloon.Tooltip
              trigger={(
                <div
                  className={'value custom'}
                  key={i}
                  onClick={() => {
                    setEditingVariable(v);
                  }}
                >
                  <SimpleLabel
                    status={'primary'}
                    key={i}
                  >
                    {v.key}
                  </SimpleLabel>
                  {v.name}
                  <ClickableIcon
                    size={'small'}
                    colorType={'danger'}
                    type={'delete'}
                    onClick={e => {
                      e.stopPropagation();
                      variables.splice(i, 1);
                      setVariables([...variables]);
                    }}
                  />
                </div>
              )}
              triggerType={'hover'}
              align={'t'}
              v2
            >
              [{t('Source')}] {t(VariableSource[v.source])}, [{t('Find')}] {v.find}, [{t('Value')}] {v.value}
            </Balloon.Tooltip>
          );
        })}
        <Button
          size={'small'}
          type={'primary'}
          onClick={() => {
            setEditingVariable({

            });
          }}
        >
          {t('Add')}
        </Button>
      </div>
    </div>
  );
};