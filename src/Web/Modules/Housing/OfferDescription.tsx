import { createElement, Fragment, type ReactNode } from "react";
import { useTranslation } from "react-i18next";
import type { components } from "@shared/api/schema";
import { Panel } from "@shared/ui/Panel";
import styles from "./OfferDetail.module.css";

type Offer = components["schemas"]["OfferListItem"];

// The formatting the portal's descriptions use, and the element each is shown as.
const formatting: Record<string, string> = {
  p: "p",
  b: "strong",
  strong: "strong",
  i: "em",
  em: "em",
  u: "u",
  ul: "ul",
  li: "li",
};

// More line breaks in a row only leave a gap (agency footers repeat them).
const maxLineBreaks = 2;

// The description is the advertiser's HTML. It is never put on the page as markup: the simple formatting
// above is rebuilt as React elements without any attributes, other elements are replaced by their text,
// and scripts and styles are dropped.
function renderDescription(html: string) {
  const body = new DOMParser().parseFromString(html, "text/html").body;
  body.querySelectorAll("script, style").forEach((element) => element.remove());
  return (body.textContent ?? "").trim() === "" ? null : renderChildren(body);
}

function renderChildren(node: Node): ReactNode[] {
  const children: ReactNode[] = [];
  let lineBreaks = 0;
  node.childNodes.forEach((child, index) => {
    if (child.nodeType === Node.TEXT_NODE) {
      const text = child.textContent ?? "";
      // Spaces and &nbsp; between line breaks do not end a run of them.
      if (text.trim() !== "") {
        lineBreaks = 0;
      }
      children.push(text);
      return;
    }
    if (child.nodeType !== Node.ELEMENT_NODE) {
      return;
    }
    const name = (child as Element).tagName.toLowerCase();
    if (name === "br") {
      lineBreaks++;
      if (lineBreaks <= maxLineBreaks) {
        children.push(<br key={index} />);
      }
      return;
    }
    lineBreaks = 0;
    const tag = formatting[name];
    children.push(
      tag ? (
        createElement(tag, { key: index }, renderChildren(child))
      ) : (
        <Fragment key={index}>{renderChildren(child)}</Fragment>
      ),
    );
  });
  return children;
}

export function OfferDescription({ offer: o }: { offer: Offer }) {
  const { t } = useTranslation("housing");
  const description = o.description ? renderDescription(o.description) : null;

  return (
    <Panel title={t("detail.description")} fill>
      {!o.detailsFetchedAt ? (
        <p className={styles.muted}>{t("detail.notFetched")}</p>
      ) : description ? (
        <div className={styles.description}>{description}</div>
      ) : (
        <p className={styles.muted}>{t("detail.noDescription")}</p>
      )}
    </Panel>
  );
}
