# Serial Commands for the Katana V2
Below is all the known commands for the Katana V2. Most likely will work with the Katana V2X and V2E. It is quite incomplete. All commands are sent over serial. Document format is inspired by [KatanaHacking](https://github.com/therion23/KatanaHacking/blob/main/USB.md?plain=1
)
## Command Structure
| Offset | Description                            |
|--------|----------------------------------------|
|      0 | Magic byte, always 0x5a                |
|      1 | Command                                |
|      2 | Length of data (including sub-command) |
|      3 | Sub-command or data                    |
|     4+ | Data                                   |

