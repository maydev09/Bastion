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

The admin rent (`rentPrice`, "czynsz") is typed the same way. On 2026-11-25 (27 751 listed offers): 87% state
one, 4% state 0 and 13% leave it empty. A stated 0 is a choice of the seller but means different things: of
1 201 such offers, 182 say "no rent" or "included" in the description, 144 name a fee there, and the rest say
nothing. Real rents range from about 2 zł/m² (a small fee) to about 50 zł/m² (luxury apartments, small studios
with utilities included); the clear mistakes are a rent equal to the price (91, typed twice), above 100 zł/m²
(13, e.g. 132 876 zł) and below 1 zł/m² (16, e.g. 20 zł). A rent above the price is often real ("odstępne"
plus a high cooperative rent). The form requires a value, so about 170 offers type 1 zł; like 0, it means
included, none or not filled in (12 sampled: 3 state the rent in the description, 5 say included, 4 nothing).
The parser keeps every value as typed. The empty field does not mean no rent: 7 of 8 sampled descriptions state
one. The description often names the admin rent ("czynsz administracyjny: 544,08 zł", "950 zł czynsz do wspólnoty") or
says there is none ("bezczynszowe", "w tym czynsz administracyjny"). Full scan of the stored detail pages
(2026-11-26, 27 714 listed offers) with the rules in `RentInDescription`: where both exist, the amount read equals
the field in 90% of offers (94% within 10%; most of the rest add parking or a repair fund to the field). Of the
1 368 offers with 0 or 1 zł, the description gives an amount for 177 and says included or none for 336; of 3 436
with an empty field, an amount for 606 and none for 269. A manual review of samples found that a 0 with a silent
description is more often no rent (about half) than a hidden one (about a fifth), while an empty field hides a rent
in about 60% of offers. The monthly cost uses these facts (`MonthlyCostService`); the stated value is never changed.

## Districts

`location.reverseGeocoding.locations` lists the levels of an offer's location: `voivodeship`, sometimes `county`
and `commune`, `city_or_village`, then `district` and `residential` (a neighborhood inside the district), each with an
`id` (the path in OtoDom's URL spelling) and a `name`. Counted on the stored listing pages of 2026-11-25 (27 759
listed offers): a district on 86%, a neighborhood on 62%. Every offer in the big cities has a district, but the
levels differ by city: Warsaw and Kraków have their 18 official districts, Wrocław, Poznań and Łódź only the 5
large historical ones (the neighborhoods are finer), Gdańsk about 35 smaller ones and no neighborhoods. Some towns
(Grudziądz, Płock) and most villages have neither. One `id` always had the same name. A neighborhood never came
without a district.
