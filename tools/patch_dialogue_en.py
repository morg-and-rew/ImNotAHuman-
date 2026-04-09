# -*- coding: utf-8 -*-
"""Patch DialogueDatabase.asset: fill 'en' from No Further Instructions ENG.txt (match by Russian text)."""
import re
import sys
from pathlib import Path


def decode_yaml_string(s: str) -> str:
    """Decode Unity YAML quoted string."""
    if s is None:
        return ""
    s = s.strip()
    if not s:
        return ""
    if s.startswith('"'):
        s = s[1:]
    if s.endswith('"'):
        s = s[:-1]
    s = s.replace("\\\\r\\\\n", "\n").replace("\\r\\n", "\n")
    out = []
    i = 0
    while i < len(s):
        if s[i] == "\\" and i + 1 < len(s):
            nxt = s[i + 1]
            if nxt == "n":
                out.append("\n")
                i += 2
                continue
            if nxt == "r":
                out.append("\r")
                i += 2
                continue
            if nxt == "t":
                out.append("\t")
                i += 2
                continue
            if nxt == '"':
                out.append('"')
                i += 2
                continue
            if nxt == "\\":
                out.append("\\")
                i += 2
                continue
            if nxt == "u" and i + 5 < len(s):
                hexpart = s[i + 2 : i + 6]
                try:
                    out.append(chr(int(hexpart, 16)))
                    i += 6
                    continue
                except ValueError:
                    pass
            if nxt == "x" and i + 4 <= len(s):
                hexpart = s[i + 2 : i + 4]
                if all(c in "0123456789abcdefABCDEF" for c in hexpart):
                    try:
                        out.append(chr(int(hexpart, 16)))
                        i += 4
                        continue
                    except ValueError:
                        pass
        out.append(s[i])
        i += 1
    t = "".join(out)
    return t.replace("\r\n", "\n").replace("\r", "\n")


def encode_yaml_string(s: str) -> str:
    if s is None:
        return ""
    parts = []
    for ch in s:
        o = ord(ch)
        if ch == "\\":
            parts.append("\\\\")
        elif ch == '"':
            parts.append('\\"')
        elif ch == "\n":
            parts.append("\\n")
        elif ch == "\r":
            parts.append("\\r")
        elif ch == "\t":
            parts.append("\\t")
        elif 32 <= o <= 126:
            parts.append(ch)
        else:
            parts.append(f"\\u{o:04x}")
    return '"' + "".join(parts) + '"'


def has_cyrillic(s: str) -> bool:
    return bool(re.search(r"[\u0400-\u04FF]", s))


def extract_field_quoted(fields: str, title: str) -> str | None:
    m = re.search(
        rf"- title: {re.escape(title)}\n\s+value:\s*\"([\s\S]*?)\"\s*\n\s+type:",
        fields,
    )
    if not m:
        return None
    return m.group(1)


def extract_field_raw(fields: str, title: str) -> str | None:
    """Empty or unquoted value."""
    m = re.search(
        rf"- title: {re.escape(title)}\n\s+value:\s*\n\s+type:",
        fields,
    )
    if m:
        return ""
    m = re.search(
        rf"- title: {re.escape(title)}\n\s+value:\s*(.*?)\n\s+type:",
        fields,
        re.DOTALL,
    )
    return m.group(1) if m else None


def get_russian_text(fields: str) -> str:
    for title in ("ru", "Dialogue Text", "Menu Text"):
        inner = extract_field_quoted(fields, title)
        if inner is not None and inner.strip():
            return decode_yaml_string('"' + inner + '"')
    return ""


def extract_en_after_pos(ref: str, start_after: int) -> str | None:
    rest = ref[start_after:]
    rest = re.sub(r"^\s+", "", rest)
    if not rest:
        return None
    lines = rest.splitlines()
    en_lines: list[str] = []
    i = 0
    while i < len(lines) and not lines[i].strip():
        i += 1
    if i >= len(lines):
        return None
    if lines[i].startswith("\t"):
        while i < len(lines):
            ln = lines[i]
            if not ln.strip():
                break
            if ln.startswith("\t"):
                st = ln.strip()
                if has_cyrillic(st):
                    break
                en_lines.append(st)
                i += 1
                continue
            st = ln.strip()
            if has_cyrillic(st):
                break
            if not st:
                break
            en_lines.append(st)
            i += 1
        if en_lines:
            return "\n".join(en_lines)
    while i < len(lines):
        ln = lines[i]
        st = ln.strip()
        if not st:
            break
        if has_cyrillic(st):
            break
        en_lines.append(st)
        i += 1
    if en_lines:
        return "\n".join(en_lines)
    return None


def find_english_for_russian(ref: str, ru: str) -> str | None:
    """Find English block in reference that follows this Russian text."""
    ref = ref.lstrip("\ufeff")
    ru = re.sub(r"\s+", " ", ru.strip())
    if len(ru) < 3:
        return None
    first_line = ru.split("\n")[0].strip() if "\n" in ru else ru
    first_line = re.sub(r"\s+", " ", first_line)
    if len(first_line) < 5:
        return None
    anchor = first_line[: min(100, len(first_line))]
    pos = ref.find(anchor)
    if pos < 0:
        pos = ref.find(first_line[: min(50, len(first_line))])
    if pos < 0:
        pos = ref.find(first_line[:40])
    if pos < 0:
        return None
    sub = ref[pos:]
    # Tab + Latin / curly quote / apostrophe (English subtitle in file)
    m = re.search(r"\n\t[\t ]*[A-Za-z\u201c\u2018\u2019'\"]", sub)
    if m:
        return extract_en_after_pos(ref, pos + m.start() + 1)
    # Blank line(s) then English paragraph (long RU block then EN)
    m2 = re.search(r"\n\n[A-Za-z\u201c]", sub)
    if m2:
        return extract_en_after_pos(ref, pos + m2.end() - 1)
    return None


def en_field_needs_replace(fields: str) -> bool:
    """True if en is empty, has CJK escapes in YAML, or decoded text contains CJK."""
    m = re.search(
        r"- title: en\n\s+value:\s*(.*?)\n\s+type: 4",
        fields,
        re.DOTALL,
    )
    if not m:
        return False
    val = m.group(1).strip()
    if not val or val == '""':
        return True
    if re.search(r"\\u[4-9][0-9a-f]{3}", val):
        return True
    if val.startswith('"'):
        dec = decode_yaml_string(val)
        if re.search(r"[\u4e00-\u9fff]", dec):
            return True
    return False


# Manual lines from No Further Instructions ENG.txt — one Dialogue Entry = one replica (one EN line, or two EN lines joined with \\n if one RU box contains two EN lines from the file).
MANUAL_EN: dict[tuple[int, int], str] = {
    (5, 1): "Ring... ring... ring...",
    (26, 1): "Thanks.",
    # Conv 6: EN file lines 28–40; one Unity subtitle = one EN line (5 = two EN lines 30+31); 36 → ids 10–12; 38 → ids 14–15
    (6, 1): "Good morning, my dear townees!",
    (6, 2): "It's 10:07 a.m., July 13, which means you're listening to",
    (6, 3): "the morning broadcast.",
    (6, 4): "Pour yourself a strong cup of coffee or something stronger!",
    (6, 5): "I'm not judging anyone!\nAnd slowly get into the new day.",
    (6, 6): "Let's try to make it productive.",
    (6, 7): "Or at least get through it with dignity.",
    (6, 8): "By the way, the weather is quite atmospheric!",
    (6, 9): "Forecasters promise rain and fog for most of the day.",
    (6, 10): "So umbrellas and raincoats are your best friends today.",
    (6, 11): "your best friends today.",
    (6, 12): "But seriously...",
    (6, 13): "Such fog is, of course, suspicious.",
    (6, 14): "Maybe the institute is testing the",
    (6, 15): "weather control device again?",
    (6, 16): "Ha-ha... just kidding. Probably.",
    (6, 17): "Stay with us — there's a lot more interesting stuff ahead.",
    # Conv 7 — ref ~46–92; one Dialogue Entry = one replica (split ref lines by / and long EN paragraphs)
    (7, 2): "Good afternoon!",
    (7, 3): "Just so you know, there's no Internet.",
    (7, 4): "Excuse me, we can't give you the order by QR. Only number codes are working.",
    (7, 5): "I'll bring out the packages. The Internet's been cutting out since this morning.",
    (7, 6): "I can't mark packages, so I only hand them out by number code.",
    (7, 7): "It's strange. And the weather is strange. Rainy and foggy in the middle of summer.",
    (7, 8): "Hi! Working?",
    (7, 9): "No.",
    (7, 10): "Hell no, I'm about to die.",
    (7, 11): "No, it's not. No connection since morning.",
    (7, 12): "I'm talking about the internet connection, you jerk.",
    (7, 13): "And the internet connection is already dead.",
    (7, 14): "Fuck, bad news.",
    (7, 15): "Watch your tongue, young boy!  What makes you think you can cut in line?",
    (7, 16): "Who cares? We may die tomorrow.",
    (7, 17): "What are you talking about?",
    (7, 18): "My dad is working in our research institute.",
    (7, 19): "Do you really need to know? Your clock is ticking.",
    (7, 20): "You are the reason why unseen forces are mad!",
    (7, 21): "It's not fog. It's ancient magic.",
    (7, 22): "My grandmother was a village witch, she smelled the magic around her.",
    (7, 26): "The aura around is negative, I can feel it in my bones.",
    (7, 27): "Relax, take it easy.",
    (7, 28): "And the fog…So thick and unnatural.",
    (7, 30): "Something is happening right now like Nolan's films. Atoms, bosons, quarks.",
    (7, 31): "But my battery is actually new and I have a choice.",
    (7, 32): "Shame on you!",
    (7, 33): "I feel it in my bones!",
    # Conv 10 — ref ~97–120 (посылка / разговор у пункта)
    (10, 1): "I'm telling you, there is something in this institute.",
    (10, 2): "See my eyes? It's red as hell. Maybe I'm..a zombie.",
    (10, 3): "He was called for an urgent meeting.",
    (10, 4): "I don't know what's going on, but I feel like it's begun.",
    (10, 5): "Well, maybe supernatural things or so?",
    (10, 6): "I don't care and need subjects of experiments, supernatural forces.",
    (10, 7): "I heard about pagans here. They practiced magic and performed rituals.",
    (10, 8): "I need the Internet.",
    (10, 9): "Well, maybe.",
    (10, 10): "Maybe, everything is alright and can be fixed.",
    (10, 11): "Dammit, who even knows? If god-knows-what will happen, I'll be in touch.",
    (10, 12): "Call me if you need. Here is my number and home address.",
    (10, 13): "Why are people so boring after their twenties?",
    (10, 14): "But if something really had happened, we would probably have been warned.",
    (10, 16): "We will see real walking deads in a few days, I guarantee.",
    (10, 17): "You see, someone called my father this morning.",
    (10, 18): "Came back late and didn't explain anything.",
    (10, 19): "He said to pack all your bags and buy products for two weeks.",
    (10, 20): "Get out of this hell together. Bye!",
    (10, 21): "I hope to die in this apocalypse when I'm only seventeen. Bye!",
    (10, 22): "Who knows?",
    (10, 23): "We're getting ready to leave town far away.",
    (10, 24): "Thanks for your phone number. See you later.",
    (10, 25): "Bye. See you later maybe.",
    (10, 26): "You are fuckin' strange, man.",
    (10, 27): "See, I've warned you.",
    (10, 28): "If you become a zombie, I will shoot you, got it?",
    (10, 29): "Bye!",
    (10, 30): "They practiced magic.",
    # Conv 12 — ref ~121–147 (полуденное радио)
    (12, 1): "Good afternoon, everyone!",
    (12, 2): "Our studio continues to receive messages",
    (12, 3): "about connection failures from our listeners.",
    (12, 4): "At least, the radio is still working, ha-ha-ha!",
    (12, 5): "I won't be surprised if our research institute",
    (12, 6): "is the cause of this inconvenience…",
    (12, 7): "Alright, time for fun facts. Do you know that",
    (12, 8): "our town had an ancient pagan sanctuary?",
    (12, 9): "Well, I didn't. I've read this fact from the book",
    (12, 10): "Legends and myths of our hometown",
    (12, 11): "written by Brian Ethan Bumble.",
    # Conv 24 — ref ~244–263 (вечернее радио)
    (24, 1): "Good evening, everyone!",
    (24, 2): "The day is coming to an end. Pretty odd day, huh?",
    (24, 3): "You have to admit, it's a strange feeling —",
    (24, 4): "No Internet all day long.",
    (24, 5): "No messages, no news feed.",
    (24, 6): "No sudden work chats at the most inopportune moments.",
    (24, 7): "Silence. Pretty creepy silence, isn't it?",
    (24, 8): "Well, never mind, tomorrow will be better!",
    (24, 9): "At least, we hope so!",
    (24, 10): "We don't have anything to do.",
    (24, 11): "Stock up on candles, ha ha ha.",
    (24, 12): "You never know.",
    (24, 14): "Maybe soon there will be more experiments not only with mobile and Internet connections.",
    (24, 15): "Just kidding! Maybe.",
    # Conv 27 — ref ~276–300 (утро 15 июля)
    (27, 1): "Good morning, dear listeners!",
    (27, 2): "Today is July 15, it's 10:13 a.m., and we are starting our morning broadcast as usual.",
    (27, 3): "It's foggy outside, so we ask drivers and pedestrians",
    (27, 4): "to be especially careful.",
    (27, 5): "There may also be power outages — nothing new, as you understand.",
    (27, 6): "But don't worry! May the Force be with you!",
    (27, 7): 'Though it\'s better to say "May the connection and electricity be with you\'.',
    (27, 8): "And now, a music break.",
    # Conv 39 — ref ~435–448 (опять вы / посылка)
    (39, 1): "Good afternoon!",
    (39, 2): "You? Again?",
    (39, 3): "You've already been here 2 hours ago. And I have given you your order.",
    (39, 4): "Oh my God! You have nothing to do? No urgent plans?",
    (39, 5): "You're playing on my nerves.",
    (39, 6): "Nonsense! It's my first time! Watch your reaction.",
    (39, 7): "Give me my order.",
    (39, 8): "Are you fucking serious?",
    (39, 9): "Give my order. Now!",
    # Conv 42 — ref ~449–450 (внутренняя реплика клерка)
    (42, 1): "Shit, I need to find something. Some like a fake order.",
    (42, 2): "He'll keep nagging us and coming to see us.",
    (42, 3): "He is also probably out of his mind",
    (42, 4): "because of all these rumors about sicknesses and Morok.",
    (42, 5): "Or he is an old idiot with dementia.",
    # Conv 37 — ref ~418–434 (вторая посылка / DnD)
    (37, 1): "This is your second order.",
    (37, 2): "Thank God we finally have something to do without the Internet.",
    (37, 3): "What have you ordered?",
    (37, 4): "Excuse me, I'm just curious.",
    (37, 5): "That's good!",
    (37, 6): "See you again, bye.",
    (37, 7): 'Some sort of a "Dungeon and Dragons" board game, but not the original one. Something similar.',
    (37, 8): "Bye.",
    # Conv 40 — ref ~466–472
    (40, 1): "Thank you a lot, kid.",
    (40, 2): "Bye, go away.",
    (40, 3): "Do not come here again.",
    (40, 4): "You know what to do with your shitty \"thank you\"?",
    (40, 5): "Wipe your \"you-know-what\". Bye.",
    (40, 6): "Goodbye.",
    # Conv 41 — ref ~451–460
    (41, 1): "See? My order in flesh and blood. And you was telling me the opposite.",
    (41, 2): "Just go away, goodbye.",
    (41, 3): "I hope you won't come again, please.",
    (41, 4): "Not-a-good bye.",
    (41, 5): "Forget the path to our delivery service and your password for the app.",
    # Conv 43 — ref ~463–464
    (43, 1): "I have nothing to give him anyway.",
    (43, 2): "Well, I have a box with some useless junk.",
    (43, 3): "I don't use it anyway, I can give it to him.",
    # Conv 44 — ref ~461–462
    (44, 1): "Well, it's not allowed.",
    (44, 2): "Though all these boozers here know each other.",
    (44, 3): "So, if I give him another drunkard's order, everything will be okay.",
    # Conv 47 — ref ~523–524
    (47, 1): "He's strange.",
    (47, 2): "And looks so familiar.",
    (47, 3): "It's such an unpleasant feeling.",
    # Conv 52 — RU lines differ from conv 51 (not the same replicas)
    (52, 1): "Some kind of outage again.",
    (52, 2): "And the time is almost up.",
    (52, 3): "So it's time to go home, eat chebupizza, and drink sugar-free cola.",
    # Second pass: splits where one EN was glued to several ids (same ref file)
    (18, 3): "Thank you. Code 8335. I know about problems with connection.",
    (18, 4): "I have one more order, but I don't know the code. I'll come later again.",
    (19, 15): "Dude, just tell me your code or go away.",
    (19, 16): "I don't need this extra chatting.",
    (20, 1): "Don't be that skeptic. The research institute is crowded with reptilians.",
    (20, 2): "They experiment on us. It's ringing in ears, don't you hear?",
    (21, 1): "Oh, I found the number for my second order.",
    (21, 2): "The Internet appeared for a few seconds.",
    (25, 1): '"I didn\'t hear she came back. And don\'t remember if she came out. Hm, strange.',
    (25, 2): 'Need to check the camera."',
    (32, 3): "We have found a corpse just right in front of your door.",
    (32, 4): "Do you know anything? Did you hear or see anything?",
    (32, 5): "No, I didn't.",
    (32, 6): "It was extremely windy outside. Maybe, an accident.",
    (34, 1): "Strange. What do you mean it was windy outside?",
    (34, 2): "It was just foggy but not windy.",
    (34, 4): "Are you okay, sir? Are you using something?",
    (34, 5): "No, I'm not. Listen, I've been working more than a month without any day-off.",
    (34, 6): "I can give all the records from cameras and just go away.",
    (45, 1): "Well, this is how I got rid of my useless tools.",
    (45, 2): "And my obsessive admirer.",
    (49, 1): "Hi, good evening!",
    (49, 2): "He felt ill and passed out.",
    (49, 3): "Please, come as fast as you can.",
    (51, 1): "I'm so sick and tired today.",
    (51, 2): "Too many extravagant personalities for one workday.",
    (51, 3): "I need to listen to the radio.",
    # Conv 29 — ref ~303–349 (спор про возврат / болезнь / туман)
    (29, 1): "Hey.",
    (29, 2): "Good afternoon. If you want to return something, I can't process it right now.",
    (29, 3): "No Internet.",
    (29, 4): "What? No way, take this shitty order, give me my money back. In cash.",
    (29, 5): "That won't work. I'm not a salesman and I can't give you your money back",
    (29, 6): "And who the hell are you then? Nobody?",
    (29, 7): "Just sitting here with those pissed-off faces. Can't do a damn thing.",
    (29, 8): "Call your boss. I'll talk to him.",
    (29, 9): "Listen, ma'am, we can't do anything.",
    (29, 11): "Without internet access, we're unable to process the return. Got it?",
    (29, 12): "Awesome! This thing made my hair fall out!",
    (29, 13): 'It says, "Guaranteed thick hair in three weeks!"',
    (29, 14): "Wow, she's loud.",
    (29, 15): "Hello",
    (29, 16): "Can you believe he doesn't want to take my order back?",
    (29, 17): "It's fake! I'm almost bald! I can't even go outside because it's a shame.",
    (29, 18): "Wow, that's news.",
    (29, 19): "When I was living in another city in 1986,",
    (29, 47): "we had an accident at the nuclear power plant.",
    (29, 20): "All the women went bald from radiation sickness.",
    (29, 21): "Shut your mouth!",
    (29, 22): "Yeah, that's right.",
    (29, 23): "After a few days or weeks they all died. No one is alive.",
    (29, 24): "They wasted away before our eyes.",
    (29, 25): "Go to hell! Everyone!",
    (29, 26): "What do think? What has happened in the world?",
    (29, 27): "Well, I don't think it's radiation sickness.",
    (29, 28): "We don't have any nuclear reactors here.",
    (29, 29): "I think it's something more…out of this world. Check the thick fog.",
    (29, 30): "Wow, and days change nights because Helios, the God of the Sun, goes to sleep.",
    (29, 31): "Are you okay?",
    (29, 32): "I'm just guessing. You never know what to believe.",
    (29, 33): "I don't think anything.",
    (29, 34): "You're all getting on my nerves, you damn speculators and conspiracy theorists.",
    (29, 35): "It's all just nonsense. We live in a part of the country where fog and rain are normal.",
    (29, 36): "Do you have some \"whose-story-is-more-creepy\" contest?",
    (29, 37): "Your truth.",
    (29, 38): "I believe in sickness.",
    (29, 39): "And what about all those women? Any other symptoms?",
    (29, 40): "Wow, I'm not a doctor, only something I noticed.",
    (29, 41): "Feeling unwell, fever, hair loss. Some had stomach problems.",
    (29, 42): "And you know, they were talking nonsense. Thoughts were confused. They became bad, stupid.",
    (29, 44): "And then bleeding. And died later. That's how it was.",
    (29, 46): "I'm almost bald!",
    (29, 45): "Oh well, why am I here? The order, code 5574.",
    # Conv 19 — ref ~194–235 (клерк / конспирология)
    (19, 2): "As soon as it appears I update.",
    (19, 3): "Everyone just believes me.",
    (19, 4): "But as for now, I just write down numbers on a paper.",
    (19, 6): "IT'S OVER! No more Internet here! We're stuck here!",
    (19, 7): "That's nonsense, don't say that.",
    (19, 8): "Look around you, kids. Problems with phone and Internet connections the whole week.",
    (19, 9): "Someone wants us to die.",
    (19, 10): "And who? Reptilians?",
    (19, 11): "Secret civilization of Antlanteans living underground?",
    (19, 12): "Of course, we are victims of",
    (19, 13): "extraterrestrial experiments.",
    (19, 14): "Do you know about that too?",
    (19, 19): "Hey, I don't want to hear your fucking nonsense at all.",
    (19, 20): "The whole room smells like vodka because of you!",
    (19, 21): "I'm working the whole month without any day-off.",
    (19, 23): "We will go bald soon, all the equipment will break down.",
    (19, 24): "Aliens are looking at us and laughing at how stupid we are.",
    (19, 25): "And I don't want to listen to some shitty alcoholics speaking about conspiracy theories.",
    (19, 26): "So I update as soon as it works.",
    (19, 1): "Thanks. And how do you mark an order as handed out if there's no Internet?",
    (19, 5): "A lack of Internet has never been a good reason not to work.",
    (8, 3): "Grab the code.",
    (9, 1): "Wrong package.",
    (11, 1): "Ring, ring, ring.",
    # Conv 14 book: ref line 161 — one sentence / one replica; id17 in Unity = English sentence 2 (Christian motifs)
    (14, 2): "They say that Morok descends upon the city",
    (14, 3): "In reality, however, Morok sends thick fog.",
    (14, 4): "There are several signs of Morok:.....",
    (14, 17): "However, I believe that this opinion is inspired by Christian motifs.",
    (14, 5): "Read",
    (14, 6): "Put it back",
    (14, 7): "Don't waste time",
    (14, 8): "What the hell did I read?",
    (14, 9): "Hell no, I don't have time to read some Bumfuck's legends.",
    (14, 10): "We don't even have McRonald's here.",
    (14, 11): '"The first sign. A thick fog descends on the city." So far, so good.',
    (14, 12): '"The second sign. People begin to confuse directions—',
    (14, 13): '"The third sign. All thoughts become jumbled and the soul aches."',
    (14, 14): '"The fourth sign. You start to lose your appearance every day.',
    (14, 16): '"It gets worse and worse every day.',
    (14, 18): "And if you manage to find your way out of Morok's tangled labyrinths,",
    (14, 19): 'home and enemy territory." Well, not bad.',
    (14, 20): "Your skin becomes flabby, your eyes turn red.",
    (14, 21): 'You start to look old, even if you are young."',
    (14, 22): 'If you sense Morok, run, reader, run."',
    (14, 23): "We live in the middle of forest.",
    (14, 24): "Even if something really did happen,",
    (14, 25): "But not in this shit.",
    (14, 26): "when there are too many unrighteous people.",
    (14, 27): "when people need to find the strength within themselves to become better.",
    (14, 28): "To take a new step on their life's journey.",
    (14, 29): "consider yourself to have accomplished the task.",
    (14, 30): "north and south,",
    (14, 31): "If you don't get out of this nightmare, you'll end up in the clutches of death.",
    (14, 32): "Rainy and foggy weather doesn't mean that we are on the edge of an apocalypse.",
    (14, 33): "Even though I only believe in nuclear explosions, leaks or so…",
    (15, 1): "Of course, sermons in the morning! This is what we actually need in this town.",
    (15, 2): "Who's even going to listen to him?",
    (15, 3): "I can hear him speaking about",
    (15, 4): "I won't be surprised if he calls himself a member of the Masonic Lodge.",
    (15, 5): "Fucking preacher.",
    (15, 6): "Reptilians and secret government brainwashing from here.",
    (16, 1): "Thank God I always keep this window closed.",
    (16, 2): "Or the whole room would stink.",
    (16, 3): "And cockroaches would be everywhere.",
    (16, 4): "How have they not gotten in here yet?",
    (17, 1): "I would also like to be in their place right now...",
    (17, 2): "Or sleep at kindergarten.",
    (17, 3): "Or better, at home.",
    (31, 1): "Hey! Need my order. Code 5574.",
    (32, 1): "Good afternoon. Captain Eagle.",
    (32, 2): "Good afternoon! How can I help you? Here for a package?",
    (32, 8): "Yes, of course.",
    (33, 4): "Wow, watch your mouth, stupid kid.",
    (36, 1): "Have you been losing your damn mind lately?",
    (36, 2): "Excuse me, what do you mean?",
    (36, 3): "You know, like feeling anxious all the time.",
    (36, 4): "No Internet, light blinking.",
    (36, 5): "I even take some medicine, but I'm feeling worse and worse.",
    (36, 6): "I can't even eat because of nausea.",
    (36, 7): "It's not worth taking pills without a prescription.",
    (36, 8): "It's not my concern but are you just pregnant?",
    (36, 9): "What? Of course, I'm not.",
    (36, 10): "It's because you spend all your time at home.",
    (36, 11): "You need to go outside and just breathe.",
    (36, 12): "Hell yeah, and get lost in the fog.",
    # Conv 46 — No Further Instructions ENG.txt ~477–522; one Dialogue Entry id = one replica from file
    (46, 1): "Good evening!",
    (46, 2): "Hey, good evening.",
    (46, 3): "We have problems with Internet connection",
    (46, 4): "so we need a code number for your order.",
    (46, 5): "Unfortunately, if the order is not paid",
    (46, 6): "I can't give you it.",
    (46, 7): "Hi. No Internet.",
    (46, 8): "Need a code number to give you your order.",
    (46, 9): "If your order is not paid, I can't give you it.",
    (46, 10): "What? Ah…Of course.",
    (46, 11): "My order is paid.",
    (46, 12): "What the hell is going on in this town?",
    (46, 13): "Got it.",
    (46, 14): "Well, my order is paid.",
    (46, 15): "Excuse me, have we met before?",
    (46, 16): "You look pretty familiar to me.",
    (46, 17): "That's good.",
    (46, 18): "Good to hear.",
    (46, 19): "You look familiar.",
    (46, 20): "It's pretty difficult to give someone an unpaid order.",
    (46, 21): "Your face does look familiar.",
    (46, 22): "You didn't come here to cause trouble, did you?",
    (46, 23): "I don't even know. I work at the lab day and night.",
    (46, 24): "I'm a university employee. So, I think we haven't met.",
    (46, 25): "Everything is paid, don't worry. I, mmm…",
    (46, 26): (
        'I\'m working for university, so as it is said "noblesse oblige". Can\'t cause trouble.'
    ),
    (46, 27): "I see.",
    (46, 28): "So far from here.",
    (46, 29): "Understood.",
    (46, 30): "Quite a trip to get here.",
    (46, 31): "So, why have you ordered here? Pretty far, huh?",
    (46, 32): "As I remember, there is a pickup point near the university.",
    (46, 33): "I could order there.",
    (46, 34): "Well, yeah. But this pickup point is on my way home.",
    (46, 35): "The pickup point near the university doesn't work any more.",
    (46, 36): "It's relatively normal here.",
    (46, 37): "I see. So, what happened at the university?",
    (46, 38): "There are so many rumors.",
    (46, 39): "Each rumor is better than the next.",
    (46, 40): "Someone tells about a hole in space.",
    (46, 41): "Someone says that it's just experiments.",
    (46, 42): "Yeah, sure.",
    (46, 43): "And they say something bad happened there. That we'll die or so.",
    (46, 44): "But I don't believe it.",
    (46, 45): "Yeah, mmm… It's okay there.",
    (46, 46): "Yes, nothing to worry about. Everything is usual.",
    (46, 47): "Nothing has happened.",
    (46, 48): "This is my code number.",
    (46, 49): "Well, okay.",
    # Conv 48 — ref ~523–622; one id = one replica (split numbered EN lines by phrase)
    (48, 1): "He's out cold, damn.",
    (48, 2): "Try to bring to life",
    (48, 3): "Call an ambulance.",
    (48, 4): "Punch in the face",
    (48, 5): "Pour an energy drink",
    (48, 6): "A-ARE Y-YOU C-CRAZ-ZY?",
    (48, 7): "Hmph…",
    (48, 8): "Where am I?",
    (48, 9): "What's happening?",
    (48, 10): "AAAAAAAAAAA….Hmph…Where am I?",
    (48, 11): "Why am I wet?",
    (48, 12): "What happened?",
    (48, 13): "I don't know. You have just fainted.",
    (48, 14): "Maybe, blood pressure or something like that.",
    (48, 15): "I don't know the reason why people faint.",
    (48, 16): "Well, just stay at home.",
    (48, 17): "Why go out anywhere?",
    (48, 18): "What if there was no one around?",
    (48, 19): "What would happen?",
    (48, 20): "It's good that you came to your senses.",
    (48, 21): "Who knows, If I was slower. Maybe, it would have been worse...",
    (48, 22): "Yeah, you're right…",
    (48, 23): "Thank you.",
    (48, 24): "I don't know, I feel sick.",
    (48, 25): "Everyone has been complaining about this lately.",
    (48, 26): "Everyone must leave, flee the town.",
    (48, 27): "It will only get worse...",
    (48, 28): "No one will remain.",
    (48, 29): "Only parched earth.",
    (48, 30): "Another guesswork enthusiast.",
    (48, 31): "I understand, I understand.",
    (48, 32): "I'll just get my salary and leave immediately.",
    (48, 33): "You are all hostages to material things.",
    (48, 34): "We only have one life.",
    (48, 35): "Enjoy it.",
    (48, 36): "Where would I go?",
    (48, 37): "I was born and raised here...",
    (48, 38): "I have nowhere to go.",
    (48, 39): "Just like all of us.",
    (48, 40): "We were born here, and we will die here.",
    (48, 41): "It's a vicious circle.",
    (48, 42): "Okay, sir. You need to go, goodbye.",
    (48, 43): "Take your order.",
    (48, 44): "If something happens I will go away from this town immediately.",
    (48, 45): "Thank you a lot for saving me.",
    (48, 46): "I owe you.",
    (48, 47): "Don't forget the order.",
    (48, 48): "God bless, everything is okay.",
    (48, 49): "Don't know, man.",
    (48, 50): "Fainting has become usual recently. Just fall and that's it.",
    (48, 51): "Maybe, something with my head?",
    # Conv 50 — ref ~531–568; ambulance / symptoms / hospital
    (50, 1): "What even happened to you, man?",
    (50, 2): "Can't you just faint at home? Ahh, shit…",
    (50, 3): "Good evening.",
    (50, 4): "What's going on here?",
    (50, 5): "Oh, I see.",
    (50, 6): "Is he breathing?",
    (50, 7): "Well, I think so.",
    (50, 8): "I was afraid to go near him. I didn't want to make it worse.",
    (50, 9): "We're not taught first aid.",
    (50, 10): "I didn't check. I called you immediately.",
    (50, 11): "I don't know the reason why he fell.",
    (50, 12): "Well, if you had checked his breathing,",
    (50, 13): "nothing terrible would have happened.",
    (50, 14): "Lately, we often have to respond to calls like this...",
    (50, 15): "Some people even die.",
    (50, 16): "Hell knows what to do.",
    (50, 17): "They say that there is a sickness in the town.",
    (50, 18): "It messes with people's minds...",
    (50, 19): "I went to the staff room and heard a noise. When I came back, he was already unconscious.",
    (50, 20): "An unpleasant situation.",
    (50, 21): "I wouldn't want people fainting or dying",
    (50, 22): "in the middle of the pickup point.",
    (50, 23): "It's as if some hidden forces are trying to mess with me.",
    (50, 24): "Have you noticed any symptoms?",
    (50, 25): "He was just strange.",
    (50, 26): "All red, twitchy.",
    (50, 27): "I'm telling you, it must have been an sickness.",
    (50, 28): "Maybe the sickness.",
    (50, 29): "But you know…",
    (50, 30): "If I were you, I'd be praying.",
    (50, 31): "You never know who's next.",
    (50, 32): "Maybe even you.",
    (50, 33): "Okay, enough with the poetry. We'll take him to the hospital.",
    (50, 34): "Well, I hope no accidents will happen. Good luck.",
    (50, 35): "Thank you. And I tell you again - don't forget to pray.",
}


def patch_database(db_path: Path, ref_path: Path) -> tuple[int, int, int]:
    text = db_path.read_text(encoding="utf-8")
    ref = ref_path.read_text(encoding="utf-8")

    entry_re = re.compile(
        r"(    - id: (\d+)\n      fields:\n)(.*?)(\n      conversationID: (\d+))",
        re.DOTALL,
    )

    matched = 0
    skipped = 0
    skipped_no_cjk = 0

    def repl_entry(mo):
        nonlocal matched, skipped, skipped_no_cjk
        prefix, fields, suffix = mo.group(1), mo.group(3), mo.group(4)
        eid = int(mo.group(2))
        conv = int(mo.group(5))

        manual = MANUAL_EN.get((conv, eid))
        if manual is not None:
            pass  # apply below
        else:
            ru_dec = get_russian_text(fields)
            if not ru_dec.strip():
                skipped += 1
                return mo.group(0)

            if not en_field_needs_replace(fields):
                skipped_no_cjk += 1
                return mo.group(0)

        if manual is not None:
            en_text = manual
        else:
            ru_dec = get_russian_text(fields)
            en_text = find_english_for_russian(ref, ru_dec)
        if not en_text or not en_text.strip():
            skipped += 1
            return mo.group(0)

        def repl_en(_):
            return f'- title: en\n        value: {encode_yaml_string(en_text)}\n        type: 4'

        new_fields, n = re.subn(
            r"- title: en\n\s+value:.*?\n\s+type: 4",
            repl_en,
            fields,
            count=1,
            flags=re.DOTALL,
        )
        if n != 1:
            skipped += 1
            return mo.group(0)
        matched += 1
        return prefix + new_fields + suffix

    new_text = entry_re.sub(repl_entry, text)
    db_path.write_text(new_text, encoding="utf-8")
    return matched, skipped, skipped_no_cjk


def main():
    root = Path(__file__).resolve().parent.parent
    ref = Path(r"c:\Users\andre\OneDrive\Рабочий стол\No Further Instructions ENG.txt")
    db = root / "Assets" / "SystemDialog" / "DialogueDatabase.asset"
    if not ref.exists():
        print("REF missing", ref)
        sys.exit(1)
    m, s, nc = patch_database(db, ref)
    print("patched (CJK en fields):", m)
    print("skipped (no match / empty):", s)
    print("skipped (en already no CJK escape):", nc)


if __name__ == "__main__":
    main()
