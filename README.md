# Bastion

## Where Bastion came from

Bastion grew out of two tools I built for myself to automate my own searches:

- **JobOffersFinder** (Python) collected job offers from several job boards. It ran on my home server and genuinely helped me land a job.
- **FlatFinder** (C#) did the same for apartment listings.

Both did their job well, but they shared one limitation: no user interface. I browsed the data directly with SQL queries. That gave me full control, but it didn't surface the most interesting part — the patterns and trends you can't see by looking at individual records.

Bastion is the next step:

- **One platform** instead of two separate projects in different tech stacks.
- **A frontend** that clearly presents the insights hidden in the collected data.
- **A modular architecture** where the scrapers are just the first modules. The long-term goal is a personal headquarters: a hub of tools that automate everyday tasks for a single person.
