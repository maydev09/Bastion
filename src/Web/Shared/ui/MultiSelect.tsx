import { Combobox } from "@base-ui/react/combobox";
import styles from "./MultiSelect.module.css";
import { Icon } from "./Icon";

type Props<Item> = {
  label: string;
  placeholder?: string;
  // The items to choose from: all of them, or the results of the caller's search (see onSearch).
  items: Item[];
  value: Item[];
  onChange: (value: Item[]) => void;
  itemToKey: (item: Item) => string;
  itemToLabel: (item: Item) => string;
  // A muted second part of an item in the list, e.g. its county.
  itemToHint?: (item: Item) => string;
  // Given when the caller searches itself (e.g. on the server); the typed text is passed on and items are not filtered here.
  onSearch?: (text: string) => void;
  // Shown when no item matches, or before anything is typed into a search.
  emptyText: string;
  // The accessible name of a chip's remove button, e.g. "Remove Gdańsk".
  removeLabel: (itemLabel: string) => string;
};

// A text field that picks several items; the picked ones show as chips inside it.
export function MultiSelect<Item>({
  label,
  placeholder,
  items,
  value,
  onChange,
  itemToKey,
  itemToLabel,
  itemToHint,
  onSearch,
  emptyText,
  removeLabel,
}: Props<Item>) {
  const isPicked = (item: Item) => value.some((v) => itemToKey(v) === itemToKey(item));
  // Picked items stay known to the combobox even when a new search does not return them.
  const known = onSearch
    ? [...items, ...value.filter((v) => !items.some((i) => itemToKey(i) === itemToKey(v)))]
    : items;

  return (
    <Combobox.Root
      multiple
      items={known}
      filteredItems={onSearch ? items : undefined}
      value={value}
      onValueChange={onChange}
      itemToStringLabel={itemToLabel}
      isItemEqualToValue={(item, picked) => itemToKey(item) === itemToKey(picked)}
      filter={onSearch ? null : (item, query) => fold(itemToLabel(item)).includes(fold(query))}
      onInputValueChange={(text, { reason }) => {
        if (onSearch && reason !== "item-press") {
          onSearch(text);
        }
      }}
    >
      {/* Combobox.Label names only a trigger button, and the input takes no id of ours, so the name is set on the input. */}
      <div className={styles.label} aria-hidden="true">
        {label}
      </div>
      <Combobox.InputGroup className={styles.group}>
        <Combobox.Chips className={styles.chips}>
          {value.map((item) => (
            <Combobox.Chip
              key={itemToKey(item)}
              className={styles.chip}
              aria-label={itemToLabel(item)}
            >
              {itemToLabel(item)}
              <Combobox.ChipRemove
                className={styles.remove}
                aria-label={removeLabel(itemToLabel(item))}
              >
                <Icon name="close" size={14} />
              </Combobox.ChipRemove>
            </Combobox.Chip>
          ))}
          <Combobox.Input
            aria-label={label}
            className={styles.input}
            placeholder={value.length > 0 ? undefined : placeholder}
          />
        </Combobox.Chips>
      </Combobox.InputGroup>
      <Combobox.Portal>
        <Combobox.Positioner className={styles.positioner} sideOffset={4} align="start">
          <Combobox.Popup className={styles.popup}>
            <Combobox.Empty className={styles.empty}>{emptyText}</Combobox.Empty>
            <Combobox.List className={styles.list}>
              {(item: Item) => (
                <Combobox.Item key={itemToKey(item)} value={item} className={styles.item}>
                  <span className={styles.indicator}>
                    {isPicked(item) && <Icon name="check" size={16} />}
                  </span>
                  <span>
                    {itemToLabel(item)}
                    {itemToHint && <span className={styles.hint}> {itemToHint(item)}</span>}
                  </span>
                </Combobox.Item>
              )}
            </Combobox.List>
          </Combobox.Popup>
        </Combobox.Positioner>
      </Combobox.Portal>
    </Combobox.Root>
  );
}

// Typed without Polish letters still matches: "slask" finds "śląskie", "lodz" finds "łódzkie".
function fold(text: string) {
  return text
    .toLowerCase()
    .replaceAll("ł", "l")
    .normalize("NFD")
    .replace(/\p{Mn}/gu, "");
}
