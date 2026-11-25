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

Except once: one offer answered `410` to three attempts (2026-11-21 and 22), yet stayed in the
listing and opened normally on 2026-11-24. So `410` is almost always final, but not always.
An offer it hides like this is marked removed and listed again by the next import or scan that sees it.

A `404` was seen once (2026-11-21), for an offer that stayed in the listing: three attempts within
a few hours answered `404`, and the next day the page answered `200` with the active offer. So a
`404` can be temporary (perhaps while the ad is edited or moderated) and is not a removal.
That is why an offer whose attempts ran out is tried again a day later (`RetryAfterHours`).

## Amenities

The detail page lists amenities as ticked values in groups, in `ad.additionalInformation`
(`{"label": "extras_types", "values": ["extras_types::balcony"]}`; security values look like
`security_types-102::monitoring`) and again in `ad.attributes`. Groups: `extras_types`,
`equipment_types`, `media_types`, `security_types`. A value is either listed or absent: there is no
"no". The listing page shows only a few of them as tags.

There is no furnished field: furniture is the value `furniture` among `equipment_types` (in older
pages also among `extras_types`). Of 19 285 detail pages (production, 2026-11-22) 80.2% list
furniture, 5.7% list other equipment without it (so the flat is unfurnished) and 14.1% list no
equipment (not stated).

Other fields of the page not stored yet: `rent_to_students` (`1`/`0`, about 20% of pages),
windows, building material and the energy certificate.

## Values chosen from a list

Values of the detail page's `characteristics` seen on the detail pages fetched on 2026-11-24 (production):
- `building_type`: `block`, `apartment`, `tenement`, `house`, `ribbon` (terraced house), `other`, `infill`,
  `loft`;
- `heating`: `urban`, `gas`, `boiler_room`, `electrical`, `other` (`tiled_stove` was seen earlier);
- `construction_status`: `ready_to_use`, `to_completion`, `to_renovation`;
- `floor_no`: `cellar`, `ground_floor`, `floor_1` … `floor_10`, `floor_higher_10` (about 2% of pages) and
  `garret`. The last two give no floor number, so the floor stays empty.

Each field is missing on some pages: the seller did not fill it in (for example the build year on about 30%
of pages, the date the flat is available on about a third).

## Values typed by sellers

Numbers in the detail page's `characteristics` are what the seller typed, unchecked. On 2026-11-23 an
offer had `build_year` = `195014` (probably 1950); it did not fit the database column and stopped the
details fetch. The parser keeps only plausible numbers (build year 1000-2100, building floors 0-100,
location radius 0-100000 m) and stores any other value as written in `housing.offer.rejected_values`, by
column name.
