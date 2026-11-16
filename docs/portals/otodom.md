# Otodom

- **Reviewed:** 2026-11-16
- **Module:** FlatOffers

Checklist: ADR 0016, "Review before adding a portal".

## Terms of service

[Terms](https://www.otodom.pl/regulamin) of Grupa OLX sp. z o.o., in force since 2024-02-17.

- §2.3 protects the content of the service, including ads: using it without written
  consent and aggregating and processing data from the service are not allowed.
- No clause mentions bots, crawlers, scraping or `robots.txt`.

## robots.txt

[robots.txt](https://www.otodom.pl/robots.txt) has one group, `User-agent: *`, with `Allow: /`.

- Search results (`/pl/wyniki/...`) and offer pages (`/pl/oferta/...`) are allowed.
- Disallowed: map view (`?...map=1`), `/oferta/kontakt/`, `/m/oferta/abuse/`.
- No `Crawl-delay`. `Content-Signal: search=yes, ai-input=yes, ai-train=no`.

## Account

Not needed: search results and offers are public. The scraper does not log in.

## Personal data

Ads of private owners can contain names, phone numbers and photos of people. The listing
data we store (title, URL, price, area, rooms) contains none; the UI links to the offer
on Otodom for contact details.

## Anti-bot measures

Reported by others: `403` for requests from data centers (e.g. GitHub Actions runners).
Not seen on our runs through the VPN.
