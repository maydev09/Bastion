import { Select as BaseSelect } from "@base-ui/react/select";
import styles from "./Select.module.css";
import { Icon, type IconName } from "./Icon";

type Props<Value> = {
  value: Value;
  // An icon before a label lets the eye find an item before reading it.
  items: { value: Value; label: string; icon?: IconName }[];
  onChange: (value: Value) => void;
  // The accessible name when no visible label is next to the select.
  label: string;
};

export function Select<Value>({ value, items, onChange, label }: Props<Value>) {
  return (
    <BaseSelect.Root
      value={value}
      items={items}
      onValueChange={(next) => {
        if (next !== null) {
          onChange(next);
        }
      }}
    >
      <BaseSelect.Trigger className={styles.trigger} aria-label={label}>
        <BaseSelect.Value>
          {(selected: Value) => {
            const item = items.find((i) => i.value === selected);
            return <ItemLabel label={item?.label ?? ""} icon={item?.icon} />;
          }}
        </BaseSelect.Value>
        <BaseSelect.Icon className={styles.icon}>
          <Icon name="chevronDown" size={16} />
        </BaseSelect.Icon>
      </BaseSelect.Trigger>
      <BaseSelect.Portal>
        <BaseSelect.Positioner className={styles.positioner} sideOffset={4}>
          <BaseSelect.Popup className={styles.popup}>
            <BaseSelect.List>
              {items.map((item) => (
                <BaseSelect.Item
                  key={String(item.value)}
                  value={item.value}
                  className={styles.item}
                >
                  <BaseSelect.ItemIndicator className={styles.indicator}>
                    <Icon name="check" size={16} />
                  </BaseSelect.ItemIndicator>
                  <BaseSelect.ItemText>
                    <ItemLabel label={item.label} icon={item.icon} />
                  </BaseSelect.ItemText>
                </BaseSelect.Item>
              ))}
            </BaseSelect.List>
          </BaseSelect.Popup>
        </BaseSelect.Positioner>
      </BaseSelect.Portal>
    </BaseSelect.Root>
  );
}

function ItemLabel({ label, icon }: { label: string; icon?: IconName }) {
  return (
    <span className={styles.label}>
      {icon && <Icon name={icon} size={16} />}
      {label}
    </span>
  );
}
