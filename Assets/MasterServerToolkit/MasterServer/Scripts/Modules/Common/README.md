# Common Module Packets

This folder contains small reusable packet types shared by multiple modules.

## Purpose

- Avoid repeating binary serialization for common pairs/lists.
- Keep packet classes simple and transport-focused.
- Provide generic packet bases such as `BaseListPacket<T>`.

## Rules

- Keep serialization order stable.
- Do not place module authority or gameplay policy here.
- Add a dedicated packet beside a module when the payload is module-specific.
- Keep common packets small enough to be safely reused without hidden side effects.
