# 0019. VPN for scraping through a WireGuard container

- **Status:** Accepted
- **Date:** 2026-11-18, updated 2026-11-19 (verified on the server, stack restarts together)

## Context

Live scraping must go out only through a VPN (SPEC, Security and privacy). On a developer machine
the VPN runs on the host and `RouteVpnCheck` asks `ip route get <host>` which interface carries the
traffic. On the server Bastion runs in Docker, where a container sees only its own `eth0`, so
the same check would always fail. The server also runs SSH and backups, which should keep using
the home connection.

## Decision

On the server a **VPN container** (gluetun, `qmcgaw/gluetun`, WireGuard) holds the tunnel, and
the Bastion container joins its network namespace (`network_mode: service:vpn`). Only Bastion's
traffic goes through the VPN; the host network is unchanged.

- The check stays as it is: inside the shared namespace `ip route get` shows the WireGuard
  interface (`wg0`, type `wireguard` or `tun`), and without a tunnel it fails closed as today.
  The app image needs `iproute2`.
- gluetun blocks all traffic outside the tunnel (kill switch), so a dropped VPN stops
  scraping instead of leaking the home IP.
- The WireGuard private key is a Docker secret (`wireguard_private_key`, read by gluetun from
  `/run/secrets`), stored with systemd-creds like the other server secrets (ADR 0010). It is a
  permanent secret: it comes from the VPN provider's account, so the setup script asks for it.
- The VPN container exists only in the server Compose override; on a developer machine the
  VPN still runs on the host.

## Alternatives considered

- **VPN on the whole host + host network mode:** the check works unchanged, but all server
  traffic (SSH, updates, backups) goes through the VPN and the container loses network isolation.
- **Compare the public egress IP with the home IP:** needs an external IP service on every run
  and knowledge of the home IP; a check of the route is simpler and works offline.
- **Plain WireGuard container (`wg-quick`):** smaller, but its config file holds the private key
  in plain text (against ADR 0010) and it has no kill switch.

## Consequences

- One more container with `NET_ADMIN` and `/dev/net/tun`; its version is pinned.
- Ports of the Bastion UI are published on the `vpn` service, because Bastion has no network
  of its own.
- Verified on the server: Bastion reaches `db` by name inside gluetun's network, and an import
  goes out through the tunnel; no extra gluetun firewall settings are needed.
- When the `vpn` container restarts, it gets a new network and the app keeps the old one, so
  the two never restart alone: systemd restarts the whole stack (`deploy/bastion.service`).
