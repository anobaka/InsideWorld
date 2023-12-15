import React, { useCallback, useRef, useState } from 'react';
import { Dialog, Select, Tag } from '@alifd/next';
import { useTranslation } from 'react-i18next';
import type { IVariable } from '../Variables';
import Variables from '../Variables';
import { availablePropertiesForProcessing, ProcessorType, PropertyProcessorTypeMap } from '../ProcessDialog/models';
import {
  bulkModificationProperties,
  BulkModificationProperty,
  resourceLanguages,
  TagAdditionalItem,
} from '@/sdk/constants';
import './index.scss';
import { useUpdate, useUpdateEffect } from 'react-use';
import BApi from '@/sdk/BApi';
import { createPortalOfComponent } from '@/components/utils';
import type { IBulkModificationProcess } from '@/pages/BulkModification';
import TextProcessor from '@/pages/BulkModification/components/ProcessDialog/Processors/TextProcessor';
import DateTimeProcessor from '@/pages/BulkModification/components/ProcessDialog/Processors/DateTimeProcessor';
import NumberProcessor from '@/pages/BulkModification/components/ProcessDialog/Processors/NumberProcessor';
import EnumProcessor from '@/pages/BulkModification/components/ProcessDialog/Processors/EnumProcessor';
import VolumeProcessor from '@/pages/BulkModification/components/ProcessDialog/Processors/VolumeProcessor';
import MultiValueProcessor from '@/pages/BulkModification/components/ProcessDialog/Processors/MultiValueProcessor';
import { Tag as TagDto } from '@/core/models/Tag';

interface IProps {
  process?: IBulkModificationProcess;
  onSubmit?: (value: IBulkModificationProcess) => any;

  variables?: IVariable[];
}

const ProcessDialog = (props: IProps) => {
  const { t } = useTranslation();
  const forceUpdate = useUpdate();

  const {
    process: propsProcess,
    onSubmit,
    variables = [],
  } = props;
  const [visible, setVisible] = useState(true);
  const [process, setProcess] = useState<IBulkModificationProcess>(propsProcess || {});
  const propertyKeyCandidatesRef = useRef<{ label: string; value: string }[]>();

  useUpdateEffect(() => {
    if (process.property == BulkModificationProperty.CustomProperty && !propertyKeyCandidatesRef.current) {
      propertyKeyCandidatesRef.current = [];
      // get all custom property keys

      BApi.resource.getAllCustomPropertyKeys().then(r => {
        const keys = r.data || [];
        propertyKeyCandidatesRef.current = keys.map(k => ({
          label: k,
          value: k,
        }));
        forceUpdate();
      });
    }
  }, [process?.property]);

  const renderCustomPropertyKeySelector = () => {
    if (process.property != BulkModificationProperty.CustomProperty) {
      return null;
    }
    return (
      <div className={'block'}>
        <div className="label">
          {t('Property Key')}
        </div>
        <div className="value">
          <Select
            style={{ width: 300 }}
            dataSource={propertyKeyCandidatesRef.current || []}
            value={process?.propertyKey}
            onChange={v => setProcess({
              ...process,
              propertyKey: v,
            })}
            useVirtual
            showSearch
          />
        </div>
      </div>
    );
  };

  const close = useCallback(() => {
    setVisible(false);
  }, []);


  console.log(process);


  const renderProcessor = () => {
    const processorType = process.property != undefined && PropertyProcessorTypeMap[process.property];
    if (processorType == undefined) {
      return null;
    }

    const setProcessValue = (value: any) => {
      setProcess({
        ...process,
        value,
      });
    };

    switch (processorType) {
      case false:
        break;
      case ProcessorType.Text:
        return (
          <TextProcessor.Editor
            variables={variables}
            onChange={setProcessValue}
          />
        );
      case ProcessorType.DateTime:
        return (
          <DateTimeProcessor.Editor
            variables={variables}
            onChange={setProcessValue}
          />
        );
      case ProcessorType.Number:
        return (
          <NumberProcessor variables={variables} />
        );
      case ProcessorType.Language:
        return (
          <EnumProcessor.Editor
            dataSource={resourceLanguages}
            variables={variables}
            onChange={setProcessValue}
          />
        );
      case ProcessorType.Originals:
        return (
          <MultiValueProcessor.Editor
            variables={variables}
            getCandidates={() => BApi.resource.getAllOriginals().then(r => (r.data || []).map(d => ({
              label: d.name!,
              value: d.id!,
            })))}
            onChange={setProcessValue}
          />
        );
      case ProcessorType.Volume:
        return (
          <VolumeProcessor
            variables={variables}
            onChange={setProcessValue}
          />
        );
      case ProcessorType.Tag:
        return (
          <MultiValueProcessor.Editor
            variables={variables}
            getCandidates={() => BApi.tag.getAllTags({ additionalItems: TagAdditionalItem.GroupName })
              .then(r => (r.data || []).map(d => {
                // @ts-ignore
                const tag = new TagDto(d);
                return {
                  label: tag.displayName,
                  value: tag.id,
                };
              }))}
            onChange={setProcessValue}
          />
        );
      case ProcessorType.Publisher:
        return (
          <MultiValueProcessor.Editor
            variables={variables}
            getCandidates={() => BApi.publisher.getAllPublishers().then(r => (r.data || []).map(d => ({
              label: d.name!,
              value: d.id!,
            })))}
            onChange={setProcessValue}
          />
        );
    }

    return null;
  };

  return (
    <Dialog
      visible={visible}
      width={'auto'}
      className={'bulk-modification-processor-dialog'}
      closeMode={['close', 'mask', 'esc']}
      onClose={close}
      onCancel={close}
      onOk={() => {
        close();
        onSubmit && onSubmit(process);
      }}
    >
      <div className="filter-form">
        <div className="block">
          <div className="label">
            {t('Property')}
          </div>
          <div className="value">
            <Tag.Group>
              {bulkModificationProperties.map(p => {
                const currChecked = process.property == p.value;
                const available = availablePropertiesForProcessing.includes(p.value);
                return (
                  <Tag.Selectable
                    key={p.value}
                    onChange={checked => {
                      if (!currChecked && checked) {
                        setProcess({
                          ...process,
                          property: p.value,
                        });
                      }
                    }}
                    checked={currChecked}
                    disabled={!available}
                  >{t(p.label)}</Tag.Selectable>
                );
              })}
            </Tag.Group>
          </div>
        </div>
        {renderCustomPropertyKeySelector()}
        <div className="block">
          <div className="label">
            {t('Variables')}
          </div>
          <div className="value">
            <Variables variables={variables} />
          </div>
        </div>
        {renderProcessor()}
      </div>
    </Dialog>
  );
};

ProcessDialog.show = (props: IProps) => createPortalOfComponent(ProcessDialog, props);

export default ProcessDialog;