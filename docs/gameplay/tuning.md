# Tuning

**Every gameplay number lives in [`tuning/tuning.tres`](../../tuning/tuning.tres)**, backed by
[`tuning/Tuning.cs`](../../tuning/Tuning.cs) and loaded by the `Tuning` autoload. Nothing else in
the code should contain damage, timings, radii or cooldowns.

Why: character owners control an ability's *shape* in code, and this file controls its
*strength*. Balancing is then a one-file change that anyone can review.

## Groups

| Group | Keys (examples) |
|---|---|
| Light / Heavy / Parry / Dash / Execute | `LightWindup`, `LightDamagePercent`, `HeavyLungeRange`, `ParryActiveWindow`, `DashCooldown`, `ExecuteBehindAngleDegrees` |
| Health & respawn | `MaxHealth`, `RespawnTime`, `RespawnTimeLastCall`, `SpawnProtectionDuration`, `DeathCamDuration` |
| Golden Knife & bounty | `GoldenKnifeFirstSpawn`, `GoldenKnifeDuration`, `GoldenKnifeScoreMultiplier`, `BountyAnnounce*` |
| Match | `ScoreTargetDuelPit`, `ScoreTargetChaos`, `MatchTimeLimitSeconds`, `LastCall*`, `ResultsScreenDurationSeconds` |
| Ability grammar limits | `MinAbilityTellTime`, `MaxControlEffectDuration`, `AbilityCooldownMin/Max` |
| Networking | `ServerTickRateHz`, `MaxRewindTimeSeconds`, `InterpolationDelaySeconds`, `PingIntervalSeconds`, `Reconciliation*` |
| Misc | `VoidCatchY` (rescue height), `HopEnabled`, `HopImpulse` |
| Per ability | `AbilityNumbers` dictionary: `"blink.range"`, `"firepatch.damage_per_second"`, … |

## Changing a number

1. Edit `tuning/tuning.tres` in the editor or as text. No code change or rebuild is needed.
2. The server's copy is what counts. Redeploy (`make deploy-server`) to change the live game.
3. Run `make test`: the ability validator re-checks the grammar limits.

**Testing tip:** to watch whole matches quickly, shorten `MatchTimeLimitSeconds`,
`LastCallTimeRemainingSeconds` and `ResultsScreenDurationSeconds` locally. **Restore them before
committing.** See [Testing](../testing.md).
