import { useTranslation } from "react-i18next";

export default function ReferenceValueCount({ count }: { count?: number }) {
  const { t } = useTranslation();

  if (count === undefined) return null;

  return (
    <span
      className="ml-1 text-xs tabular-nums opacity-70"
      title={t("property.reference.resourceCount", { count })}
    >
      ({count.toLocaleString()})
    </span>
  );
}
