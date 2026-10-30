# בנייה מחדש

## התוכנה (openscq30-gui.exe ו-openscq30.exe)

הבסיס הוא OpenSCQ30 גרסה 2.13.0 בתוספת PR #414 (ה-commit `83144843bbcaf23884e8d134b2b66bb2925d8310`). מעליו מחילים את `hebrew-and-d1203-state.patch`.

נדרש Rust 1.99.1 (הגרסה שב-`.build-tool-versions` של הפרויקט).

```
git clone https://github.com/Oppzippy/OpenSCQ30.git
cd OpenSCQ30
git fetch origin pull/414/head
git checkout 83144843bbcaf23884e8d134b2b66bb2925d8310
git apply path\to\hebrew-and-d1203-state.patch
cargo build --profile release-fast --package openscq30-gui --package openscq30-cli
```

הקבצים נוצרים ב-`target\release-fast\`.

הבדיקות של Liberty 5 Pro (דגם D1203):

```
cargo test --package openscq30-lib d1203
```

אם ה-commit כבר לא זמין ב-GitHub, למשל כי ה-PR עודכן, קוד המקור המלא אחרי החלת השינויים מצורף לכל גרסה בדף הגרסאות (`OpenSCQ30-hebrew-source.zip`).

### מה ה-patch משנה

- `gui/i18n/he` ו-`lib/i18n/he` משלימים ומתקנים את התרגום לעברית.
- `lib/src/devices/soundcore/d1203*` קוראים את ההגדרות האמיתיות מחבילת המצב של האוזניות. פוענח על קושחה 05.33:

  | תג | תוכן |
  |---|---|
  | 36 / 37 / 38 | מצב שמע, רמת סינון ומצב תחבורה |
  | 26 | ביטים: צליל נגיעה, התראת סוללה, צליל הכנסה לאוזן |
  | 27 | ביטים: LDAC 2, רוח 11, Easy Chat 14, Soundstage 29 (רק כשהתג באורך 4 בתים) |
  | 25 | כיבוי אוטומטי |
  | 13–20 | הנגיעות |
  | 8 | סוללת הקופסה (בית 1) |
  | 44 | חיבור לשני מכשירים (קריאה בלבד) |

  "שמע מרחבי" מוסתר בקושחה 05.33, כי היא לא תומכת בו.
- בבדיקה שמשתמשת בחבילת מצב אמיתית, המספר הסידורי וכתובת הבלוטות' הוחלפו באפסים. לבדיקות זה לא משנה, והן לא נכללות בקובצי התוכנה.

## תוכנת הקיצורים (SoundcoreHotkeys.exe)

נבנית במהדר C# שמגיע עם Windows (‏.NET Framework 4):

```
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /codepage:65001 /out:SoundcoreHotkeys.exe SoundcoreHotkeys.cs
```

התוכנה מחפשת את `openscq30.exe` באותה תיקייה, ואת כתובת האוזניות היא קוראת מרשימת המכשירים של OpenSCQ30 (`paired-devices list --json`).
