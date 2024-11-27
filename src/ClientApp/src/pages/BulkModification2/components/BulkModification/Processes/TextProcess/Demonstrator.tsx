import { Trans, useTranslation } from 'react-i18next';
import React from 'react';
import type { TextProcessOptions } from './models';
import type { IProperty } from '@/components/Property/models';
import { TextProcessingOperation } from '@/sdk/constants';
import type { BulkModificationVariable } from '@/pages/BulkModification2/components/BulkModification/models';
import {
  ValueWithMultipleTypeDemonstrator,
} from '@/pages/BulkModification2/components/BulkModification/ValueWithMultipleType';
import { buildLogger } from '@/components/utils';

type Props = {
  property: IProperty;
  operation?: TextProcessingOperation;
  options?: TextProcessOptions;
  variables?: BulkModificationVariable[];
};

const log = buildLogger('TextProcess');

export default (props: Props) => {
  const {
    operation,
    options,
    variables,
    property,
  } = props;
  const { t } = useTranslation();

  log(props);

  switch (operation!) {
    case TextProcessingOperation.SetWithFixedValue: {
      return (
        <>
          <Trans
            i18nKey={'BulkModification.Processor.Demonstrator.Operation.SetWithFixedValue'}
          >
            <div className="primary" />
            with fixed value
            <ValueWithMultipleTypeDemonstrator
              property={property}
              valueType={options?.valueType}
              value={options?.value}
              variables={variables}
            />
          </Trans>
        </>
      );
    }
    // case TextProcessingOperation.AddToStart:
    // case TextProcessingOperation.AddToEnd:
    //   return (
    //     <Trans
    //       i18nKey={'BulkModification.Processor.Demonstrator.Operation.AddToStartOrEnd'}
    //       values={{
    //         direction: value.operation == TextProcessingOperation.AddToStart ? t('Position.Beginning') : t('Position.End'),
    //         value: value.value,
    //       }}
    //     >
    //       Add
    //       <span className="secondary">{value.value}</span>
    //       to
    //       <span className="primary">beginning or end</span>
    //     </Trans>
    //   );
    // case TextProcessingOperation.AddToAnyPosition:
    //   return (
    //     <Trans
    //       i18nKey={'BulkModification.Processor.Demonstrator.Operation.AddToAnyPosition'}
    //       values={{
    //         direction: value.reverse ? t('Position.End') : t('Position.Beginning'),
    //         position: value.position,
    //         value: value.value,
    //       }}
    //     >
    //       Add
    //       <div className="secondary">{value.value}</div>
    //       to the
    //       <div className="secondary">{value.position}</div>
    //       position from the
    //       <div className="primary">end</div>
    //     </Trans>
    //   );
    // case TextProcessingOperation.RemoveFromStart:
    // case TextProcessingOperation.RemoveFromEnd:
    //   return (
    //     <>
    //       <Trans
    //         i18nKey={'BulkModification.Processor.Demonstrator.Operation.RemoveFromStartOrEnd'}
    //         values={{
    //           direction: value.operation == TextProcessingOperation.RemoveFromStart ? t('Position.Beginning') : t('Position.End'),
    //           count: value.count,
    //         }}
    //       >
    //         Remove
    //         <span className="secondary">{value.count}</span>
    //         characters from
    //         <span className="primary">beginning or end</span>
    //       </Trans>
    //     </>
    //   );
    // case TextProcessingOperation.RemoveFromAnyPosition: {
    //   const texts = {
    //     direction: value.reverse ? t('Position.End') : t('Position.Beginning'),
    //     position: value.position,
    //     count: value.count,
    //     removeDirection: value.removeBefore ? t('TextOperation.Backward') : t('TextOperation.Forward'),
    //   };
    //   return (
    //     <>
    //       <Trans
    //         i18nKey={'BulkModification.Processor.Demonstrator.Operation.RemoveFromAnyPosition'}
    //         values={texts}
    //       >
    //         {/* delete 6 characters forward from the fifth character from the end */}
    //         Delete
    //         <span className="secondary">{texts.count}</span>
    //         characters
    //         <span className="primary">{texts.removeDirection}</span>
    //         the
    //         <span className={'secondary'}>{texts.position}</span>
    //         character from the
    //         <span className="primary">{texts.direction}</span>
    //       </Trans>
    //     </>
    //   );
    // }
    // case TextProcessingOperation.ReplaceFromStart:
    // case TextProcessingOperation.ReplaceFromEnd: {
    //   const texts = {
    //     direction: value.operation == TextProcessingOperation.ReplaceFromEnd ? t('Position.End') : t('Position.Beginning'),
    //     replace: value.replace,
    //     find: value.find,
    //   };
    //   return (
    //     <>
    //       <Trans
    //         i18nKey={'BulkModification.Processor.Demonstrator.Operation.ReplaceFromStartOrEnd'}
    //         values={texts}
    //       >
    //         {/* Replace xxx with yyy from start */}
    //         {/* 0 */}
    //         <div className="primary">Replace</div>
    //         {/* 1 */}
    //         <div className="secondary">{texts.find}</div>
    //         {/* 2 */}
    //         with
    //         {/* 3 */}
    //         <div className={'secondary'}>{texts.replace}</div>
    //         {/* 4 */}
    //         from
    //         {/* 5 */}
    //         <div className="primary">{texts.direction}</div>
    //       </Trans>
    //     </>
    //   );
    // }
    // case TextProcessingOperation.ReplaceFromAnyPosition: {
    //   const texts = {
    //     direction: value.reverse ? t('end') : t('start'),
    //     replace: value.replace,
    //     find: value.find,
    //   };
    //   return (
    //     <Trans
    //       i18nKey={'BulkModification.Processor.Demonstrator.Operation.Replace'}
    //       values={texts}
    //     >
    //       {/* Replace xxx with yyy */}
    //       {/* 0 */}
    //       <div className="primary">Replace</div>
    //       {/* 1 */}
    //       <div className="secondary">{texts.find}</div>
    //       {/* 2 */}
    //       with
    //       {/* 3 */}
    //       <div className={'secondary'}>{texts.replace}</div>
    //     </Trans>
    //   );
    // }
    // case TextProcessingOperation.ReplaceWithRegex: {
    //   const texts = {
    //     replace: value.replace,
    //     find: value.find,
    //   };
    //   return (
    //     <Trans
    //       i18nKey={'BulkModification.Processor.Demonstrator.Operation.ReplaceWithRegex'}
    //       values={texts}
    //     >
    //       {/* Use regex to replace xxx with yyy */}
    //       {/* 0 */}
    //       Use
    //       {/* 1 */}
    //       <div className="primary">regex</div>
    //       {/* 2 */}
    //       to
    //       {/* 3 */}
    //       <div className="primary">replace</div>
    //       {/* 4 */}
    //       <div className="secondary">{texts.find}</div>
    //       {/* 5 */}
    //       with
    //       {/* 6 */}
    //       <div className={'secondary'}>{texts.replace}</div>
    //     </Trans>
    //   );
    // }
    // case TextProcessingOperation.Delete: {
    //   return (
    //     <div className={'primary'}>
    //       {t('Remove')}
    //     </div>
    //   );
    // }
    default:
      return (
        <>
          {t('Unsupported value')}
        </>
      );
  }
};
