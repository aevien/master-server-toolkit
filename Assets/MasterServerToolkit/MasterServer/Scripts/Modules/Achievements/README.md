# Achievements Module

This module provides generic achievement progress and unlock messaging.

## Main Types

- `AchievementsModule` is the authoritative server module.
- `AchievementsModuleClient` is the client facade for progress/unlock events.
- `AchievementsModuleServer` is the server-side facade used by trusted server components.
- `AchievementData` and `AchievementsDatabase` describe available achievements.
- `AchievementProgressInfo` and `UpdateAchievementProgressPacket` are wire payloads.
- `AchievementGiver` is a demo/helper base for awarding progress from Unity behaviours.

## AchievementData Inspector

`Key` is the persisted identity and must remain stable after release. Title, description and result
may be localization keys or final text according to the consuming game UI. `Required Progress` is the
authoritative unlock threshold; `0` or a negative value makes the first accepted update satisfy it.
`Hidden` affects presentation only. Dependencies must form an acyclic
graph. Extra parameters and result commands are project-defined key/string payloads that MST preserves
without interpreting. Reward command handlers must remain idempotent because pending rewards may retry.

## Module Inspector

- `Client Can Update Progress` permits authenticated clients to update only their own achievement
  profile. Trusted room/server updates use the separate server API.
- `Achievements Database` is the authoritative catalog used to create profile entries, validate keys
  and execute result commands. Assign the same catalog expected by the client UI.
- `AchievementGiver.Achievements` limits a derived gameplay giver to its configured definitions unless
  that derived component deliberately resolves achievements another way.

## Rules

- Client updates always target the authenticated client's own profile. When the profile is owned by
  a room, the master forwards the request and returns the room result to the client.
- Server-side achievement updates require the exact `room_server` permission key. A numeric
  permission level or account-admin status does not grant this capability.
- Achievement rewards and unlock notifications are emitted only for the authoritative profile
  transition from locked to unlocked. Replayed room updates and already unlocked profiles do not
  execute rewards again.
- Rewards are applied to the authoritative profile and therefore do not depend on the client still
  being logged in. A room update may finish during the profile unload grace period.
- `AchievementProgressInfo.unlockedAt` stores the unlock time as Unix milliseconds. A value of `0`
  means the achievement has not been unlocked yet. The timestamp is assigned when progress first
  reaches the required value and is not changed by reward retries.
- Reward completion is stored separately in `rewardApplied`. An unlocked achievement with
  `rewardApplied == false` remains pending and is retried when the profile is loaded again.
- Legacy JSON dates remain readable and their timezone-less values are interpreted as UTC, matching
  the old writer. A normal legacy date is treated as an already handled reward; an unlocked legacy
  year-9999 record receives the conversion time and remains pending. The binary packet layout changed
  and requires client, room and master builds to be updated together.
- A successful progress response contains a boolean: `false` means progress was accepted without
  unlocking, and `true` means this request unlocked the achievement.
- `OnAchievementUnlocked` is raised only by the server push message, never by a request ACK.
- `GetProgresses()` resolves the current loaded profile and returns its achievement progress sequence.
  It returns an empty sequence while the profile is unavailable; keep the result in a local variable when
  one UI refresh needs to enumerate it more than once.
- Use `UpdateProgressWithResult(...)` when the caller needs the progress/unlock result. Existing
  `UpdateProgress(...)` overloads still send a confirmed request but do not expose its completion.
- Keep achievement definitions generic ScriptableObjects; game-specific award policy belongs outside MST.
- Do not change progress packet serialization order without updating all MST binaries and focused
  serialization tests together.
- Avoid doing database writes from Unity scene callbacks without considering lifecycle/retry behavior.
