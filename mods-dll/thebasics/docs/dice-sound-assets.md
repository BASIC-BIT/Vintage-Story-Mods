# Dice roll sound assets

Dice roll recordings by Tagwin, used with permission. Permission provenance: the owner's September 17, 2026 report states that Tagwin recorded and offered these sounds for use. This records that attribution and provenance; it does not assert a separate license.

Source files are `C:\Users\steve\Downloads\diceroll1.ogg` through `diceroll9.ogg`. The originals were preserved and were not edited. Their SHA256 hashes were checked against the audio preflight before encoding.

## Original recordings

| File | SHA256 | Codec | Channels | Sample rate | Duration (s) | Mean (dB) | Peak (dB) |
|---|---|---|---:|---:|---:|---:|---:|
| diceroll1.ogg | EE487DBDAE6CEB818C99DD0E0F14AD264E2E33BCBBB0DD782945E7E0E490E68F | Vorbis | 1 | 48000 | 0.339583 | -26.5 | -3.0 |
| diceroll2.ogg | 1898C774DB430C7284FD68A1542C891482467A53CB254A24C4EAEF6C09D2BC80 | Vorbis | 1 | 48000 | 0.258333 | -25.5 | -0.0 |
| diceroll3.ogg | 33BDBB0C455108557902D003FCCFFD3C485979F8BA49CAA0C21613DFC66EC5A2 | Vorbis | 1 | 48000 | 0.268750 | -27.5 | -5.7 |
| diceroll4.ogg | 1A50FCA3BD89196A03717266F1CA04DDE48578722416A4C2475D0E805F199151 | Vorbis | 1 | 48000 | 0.322917 | -25.7 | -0.0 |
| diceroll5.ogg | 29535486963371DDA1A3B96AFA28B024033A6A065B40D625E61C8CC457ED35CC | Vorbis | 1 | 48000 | 0.325000 | -29.3 | -5.6 |
| diceroll6.ogg | 130551625F299C69CAFD4FE5003F0F26170FDF0BDA25B3529889ACA3A178BADB | Vorbis | 1 | 48000 | 0.329167 | -25.5 | -0.1 |
| diceroll7.ogg | E7F6BA815961C2B1492264AC3770269ACD72FA786D33FFA36029190A72A14E9F | Vorbis | 1 | 48000 | 0.391667 | -27.1 | -0.5 |
| diceroll8.ogg | A7022B4DBA00BEF55BB018E9CC99005B0BB1F9A9A85E8284079A0291375D4990 | Vorbis | 1 | 48000 | 0.437500 | -31.9 | -8.7 |
| diceroll9.ogg | 77211B52FA72E670AFFADD5C3705E805EF102C6052114BCE61CFDEE826D12909 | Vorbis | 1 | 48000 | 0.316667 | -30.8 | -3.4 |

## Packaged recordings

Each output was encoded once from its original with FFmpeg `volume`, mono 48 kHz Vorbis (`libvorbis`, quality 5). No compression, pitch changes, looping, or other effects were applied. Mean and peak levels below were measured by decoding each packaged file with FFmpeg `volumedetect`; every file also passed a full decode.

| Asset | Source | Gain (dB) | SHA256 | Codec | Channels | Sample rate | Duration (s) | Mean (dB) | Peak (dB) |
|---|---|---:|---|---|---:|---:|---:|---:|---:|
| `thebasics:sounds/dice/diceroll1` | diceroll1.ogg | -1.5 | 03159F20641EB833516648419F474714F18ECD1C339E4AB85947723CF92EF10D | Vorbis | 1 | 48000 | 0.339583 | -28.3 | -4.7 |
| `thebasics:sounds/dice/diceroll2` | diceroll2.ogg | -2.5 | 62A770D8FA703D5C9A162A4984271B4CBF95854D2E024146F6EE1940A8743493 | Vorbis | 1 | 48000 | 0.258333 | -28.2 | -2.1 |
| `thebasics:sounds/dice/diceroll3` | diceroll3.ogg | -0.5 | 765C7E522B694463EB4D0127E61DE88FD41589F0B876188929AA7CE176560385 | Vorbis | 1 | 48000 | 0.268750 | -28.1 | -6.4 |
| `thebasics:sounds/dice/diceroll4` | diceroll4.ogg | -2.3 | C380E5B0C8F450C890FACAFAACDBBCCE0E2B5C193DFA9AEDF38B6E7B9D193B38 | Vorbis | 1 | 48000 | 0.322917 | -28.1 | -2.7 |
| `thebasics:sounds/dice/diceroll5` | diceroll5.ogg | 1.3 | D8454F9B4C744E5D2DAF54C0724D29AC5B26862B6BB6DF442BACFFCF20EB2E40 | Vorbis | 1 | 48000 | 0.325000 | -28.1 | -4.1 |
| `thebasics:sounds/dice/diceroll6` | diceroll6.ogg | -2.5 | BFB2103E94067A5FADEF51016011D84F2A39578233FE33453E34FB733077A969 | Vorbis | 1 | 48000 | 0.329167 | -28.2 | -3.0 |
| `thebasics:sounds/dice/diceroll7` | diceroll7.ogg | -0.9 | BD95B8F2911CBBD773A9E615E6CCAC8CFC7424834CBB35E7E3EF4A2E1F8A440E | Vorbis | 1 | 48000 | 0.391667 | -28.2 | -1.9 |
| `thebasics:sounds/dice/diceroll8` | diceroll8.ogg | 3.9 | 48FF8CD3E8964CB79260533C213460A803C5E3A81FD833E2190B15BE1C7A7859 | Vorbis | 1 | 48000 | 0.437500 | -28.0 | -4.7 |
| `thebasics:sounds/dice/diceroll9` | diceroll9.ogg | 2.4 | 78E37A80CDEC40CC2A6E59BA344D78FBB536A7054436A83E8520764F9832FD60 | Vorbis | 1 | 48000 | 0.316667 | -28.6 | -1.7 |
