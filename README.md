# Hookivery

Plataforma de entrega de webhooks (Webhook Delivery Platform) em C# / .NET 10.
Recebe eventos de sistemas emissores, persiste-os em PostgreSQL, processa-os de forma assíncrona e entrega-os
a destinos externos com retries, idempotência, Dead Letter Queue, Outbox Pattern, observabilidade e deployment em Docker.


