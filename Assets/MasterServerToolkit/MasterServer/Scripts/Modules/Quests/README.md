# Quests Module

This module contains generic quest data and quest progress packet types.

## Main Types

- `QuestsModule` is the server module shell.
- `QuestsModuleClient` and `QuestsModuleServer` are facades for future quest requests.
- `QuestData` and `QuestsDatabase` define quest catalog data.
- `IQuestInfo`, `QuestStatus`, `QuestState` and `QuestProgressInfo` describe quest state/progress.

## QuestData Inspector

`Key` is a generated persistent identity and must not change after release. Text fields may contain
localization keys or final text according to the client. `Required Progress` is interpreted by
project-specific objective code. The time limit is minutes: `0` is unlimited and `43,200` is 30 days.
One-time behavior, parent prerequisites and child unlocks require game-owned persistence/policy; keep
parent/child relationships acyclic.

## Module Inspector

`Client Can Update Progress` and `Quests Databases` are extension settings on the current module shell.
The built-in module does not register its client quest handlers or enumerate the catalogs yet, so these
values have no runtime effect until an implementation completes that flow. MST does not infer
objective, reward or lifecycle behavior from quest assets.

## Rules

- Keep concrete quest objectives and reward policy outside MST core.
- Add request handlers only when the authority model is clear.
- Do not mutate profile quest properties from multiple threads without snapshot/lock rules.
- Keep quest ids stable once stored in profiles or databases.
