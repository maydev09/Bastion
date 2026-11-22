# Otodom

- **Reviewed:** 2026-11-16
- **Module:** Housing

Checklist: `AGENTS.md`, "Data and privacy".

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

In `__NEXT_DATA__` each listing item has `advertOwner` (name, contacts, photo) and
`organisationAssignedMember` (first and last name, ids and photo of an agency's agent);
`shortDescription` is free text. `pageProps` also holds our own `userSessionId`,
`unconfirmedUserAuthToken` and the ad-tracking `cookiesCategorisation.dfp_user_id`. The offer cards in the HTML repeat the names. `OtoDomAnonymizer`
masks the names, photos, contacts in free text and session values before a raw page is stored;
the other fields of these objects (ids, types) stay.

## Anti-bot measures

Reported by others: `403` for requests from data centers (e.g. GitHub Actions runners).
Not seen on our runs through the VPN.

## Removed offers

The page of a removed offer answers `410 Gone` (seen 2026-11-22): no redirect, a page saying the
ad is no longer available (the property may have a new owner or tenant) and similar offers below.
Offers listed in the nightly scan answered so the same day. The listing never showed an offer
again after its page answered `410` (checked 2026-11-22), so `410` is final.

A `404` was seen once (2026-11-21), for an offer that stayed in the listing: three attempts within
a few hours answered `404`, and the next day the page answered `200` with the active offer. So a
`404` can be temporary (perhaps while the ad is edited or moderated) and is not a removal.
