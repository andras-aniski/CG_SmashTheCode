# Copilot Instructions

## Copilot goals

- Read the below description and learn from it. The description is a project overview, build and run instructions, and architecture notes for a CodinGame bot.
- Implement the `Engine` project, which is currently a placeholder. The Engine should be a reusable game engine that can be used by both Bot0 and Bot1. It should implement the game logic, including the grid representation, move generation, and score calculation. The user should be able to execute the Engine project to run a local game between the chosen bots (such as Bot0 and Bot1) for testing, investigation, and development purposes.
- Bot1 is just an enemy, don't modify that code. It is just an example bot that works well with the original game engine and contains a simulation logic that can be used for testing the Engine implementation.
- Bot0 is the primary bot, and you should modify it as needed to integrate with the Engine and implement your AI logic.
- Bot0 should log to the error stream (stderr) for debugging purposes, but should not print anything to the standard output (stdout) except for the required game instructions. The stderr log should be so detailed that it can be used to understand the bot's decision-making process and diagnose any issues (I might provide you logs from external (official) arenas for analysis).
- The ultimate goal is to beat the enemy bot (Bot1) with Bot0 in like 80% of the games.

## Project

CodinGame bot for [Smash The Code](https://www.codingame.com/multiplayer/bot-programming/smash-the-code) — a Puyo Puyo-style puzzle game. The goal is to build an AI that outplays an opponent on a 12×6 grid by chaining colored block combos to send skull lines.

## Build & Run

```sh
Copilot, populate it with instructions on how to build and run the project, including any dependencies or setup steps required.
```

There are no automated tests.

## Architecture

Three projects, each compiles to a single executable:

- **Bot0** — the primary, fully-featured bot (submit `Bot0/Program.cs` to CodinGame)
- **Bot1** — minimal starter bot (CodinGame scaffold)
- **Engine** — placeholder, not yet implemented

### CodinGame constraint

CodinGame only accepts a **single `.cs` file** per submission. All bot logic for Bot0 lives entirely in `Bot0/Program.cs` with no namespaces and no dependencies.

### Game constants

```
Grid:          12 rows × 6 columns
Empty cell:    9  (CELL_EMPTY)
Skull:         0  (CELL_SKULL)
Colors:        1–5
Pairs ahead:   8  (ADVANCE_TURNS)
Score formula: (10 × B) × (CP + CB + GB)  — chain power × color bonus × group bonus
Nuisance:      score / 70 → skull lines sent (NUISANCE_DIVISOR = 70, SKULL_AMOUNT = 6)
```

### Rotation encoding

| Rotation | Block A | Block B |
|----------|---------|---------|
| 0 | col `c` | col `c+1` (A left of B) |
| 1 | col `c` | col `c` (A on top) |
| 2 | col `c` | col `c-1` (A right of B) |
| 3 | col `c` | col `c` (B on top, swapped) |

Rotations 0 and 2 are skipped at the boundary columns. Rotations 2 and 3 are skipped when both blocks in a pair have the same color.

### Timeout handling

- Player search: 94 ms
- Timeout checks should be done effectively as if a bot times out, that loses immediately.

## Codingame Description

The goal: Defeat your opponent by grouping colored blocks.

### Rules

** Constraints **

- `1` ≤ **colorA** ≤ `5`
- `1` ≤ **colorB** ≤ `5`
- `0` ≤ **x** < `6`
- Response time per turn ≤ `100ms`

Each player plays in their own grid `6` cells wide and `12` cells high. Every turn, both players are given two connected blocks they must place inside their grid. This is called a pair. The blocks work as follows:

- A pair of blocks can be rotated before being dropped into the grid. Once dropped, the blocks are no longer considered connected.
- Every block has a random color. The colors are labeled `1` to `5`.
- You are given the next pairs of blocks to place `8` turns in advance.
- Both players are given the same pairs of blocks.

The placement of blocks works as follows:

- The blocks are dropped into the grid from above, and stop once they reach the bottom or another block in the same column.
- When `4` or more blocks of the same color line up adjacent to each other, they disappear. Blocks connect horizontally or vertically, but not diagonally. The whole group need not be a straight line.
- When all groups of the grid have cleared, the blocks above them all fall until they reach the bottom or another block. If this causes new groups to form, those groups will also disappear. This is called a **chain**.

Aiming for large chains increase your offensive power and lets you automatically attack your opponent.

Attacking works as follows:

- As you create groups and perform chains, you will generate **nuisance points**. As soon as you have enough nuisance points, Skull blocks will appear in the opponent's grid.
- **Skull blocks** act like colored blocks but they will not disappear when they form a group. They are only removed from the grid if a colored block is cleared next to them.
- The Skull blocks are dropped into the player's grids in a line, 6 at a time, one in each column.
- Skull blocks are labeled `0`.

Groups and chains give **score points** and **nuisance points**.
The score points are used as a tie break if both players are still alive after `200` turns.

On each turn, you may have cleared one or more groups causing or not a chain reaction. Each moment a set of groups gets cleared in one go is called a step.
Score Points:

- The points you score each turn are calculated per step with the following formula: `score = (10 * B) * (CP + CB + GB)` where
  - **B** is the number of blocks cleared on this step.
  - **CP** is the chain power, starting at `0` for the first step. It is worth `8` for the second step and for each following step it is worth twice as much as the previous step.
  - **CB** is the Color Bonus, depending on how many different color blocks were cleared in the step. It is worth
    - `0` for **1** color
    - `2` for **2** colors
    - `4` for **3** colors
    - `8` for **4** colors
    - `16` for **5** colors
  - **GB** is the Group Bonus, depending on how many blocks are destroyed per group. When several groups have been cleared, the bonuses of all groups are summed. It is worth
    - `0` for **4** blocks
    - `1` for **5** blocks
    - `2` for **6** blocks
    - `3` for **7** blocks
    - `4` for **8** blocks
    - `5` for **9** blocks
    - `6` for **10** blocks
    - `8` for **11** or more blocks
- The value of (**CP** + **CB** + **GB**) is limited to between 1 and 999 inclusive.

Nuisance points:

- Each turn, you generate the number of score points divided by `70`​ in nuisance points. Those points are added to the previous turn's nuisance points. If this turns nuisance points are greater than `6`​, a line of skull blocks is dropped into the opponents grid for every six nuisance points. For each line, the six points are removed from the total nuisance points.
For example, if you generate **14.5** nuisance points, on the beginning of the next turn **2** rows of skull blocks will appear in your opponent's grid and you will have **2.5** nuisance points left over for this turn.

The program must first read within an infinite loop, read the contextual data from the standard input and provide to the standard output the desired instructions.
 
### Victory Conditions

Survive your opponent or have a higher score than them after the time limit.
 
### Lose Conditions

- Your program provides incorrect output.
- Your program times out.
- You fail to place the pair of blocks into free cells of your grid.

### Game Input

**Input for one game turn**

- First `8` lines: 2 space separated integers **colorA** and **colorB**: The colors of the blocks you must place in your grid by order of appearance.
- Next line: Your current score.
- Next `12` lines: `6` characters representing one line of your grid, top to bottom.
- Next line: The score of your opponent.
- Next `12` lines: `6` characters representing one line of the opponent's grid, top to bottom.

Each line of the grid can contain the characters:

- `.` for an empty cell.
- `0` for a skull block.
- An integer from `1` to `5` for a colored block.

**Output for one game turn**

- A single line: `2` space separated integers: **x** for the column in which to drop your pair of blocks. **rotation** the rotation of said pair of blocks.

For a pair of blocks such as `1` and `4` **rotation** can take these values:
- `0` for the first block on the left and the second on the right.
- `1` for the second block on top and the first on the bottom.
- `2` for the first block on the right and the second on the left.
- `3` for the first block on top and the second on the bottom.
