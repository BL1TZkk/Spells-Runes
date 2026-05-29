using System.Collections.Generic;

namespace SpellsAndRunes.Lore;

public record ScrollEntry(string Id, string Title, string Author, string Element, string Text);

public static class ScrollRegistry
{
    private static readonly Dictionary<string, ScrollEntry> _scrolls = new();

    static ScrollRegistry()
    {
        // ── Fire: Ashveil Brotherhood ─────────────────────────────────────────
        Register(new ScrollEntry("ashveil-1", "Record of Internal Affairs I", "The Ashveil Brotherhood — author removed", "Fire",
            "…do not allow it to cool. Remember this during the entire experiment.\n" +
            "\n" +
            "Only tongs. Iron container sealed for transportation. Grind the hot material into powder (the powder adheres best at high temperatures). Powder is red, coarse; handle with cloth. When boiling in sulfuric acid, color changes as follows – orange, amber; consistent with the original crystal. Any powder or liquid produced which appears dark has been improperly processed. Begin anew.\n" +
            "\n" +
            "Each member prepares his own paste individually. Paste is not provided for you. There is no storage for paste. Make paste when needed using materials available to you.\n" +
            "\n" +
            "Describe what you are doing here inside these walls. Write about it somewhere else where other people cannot read about it. What you are holding now would not exist if we did not decide you had proved yourself worthy of holding it. Our decision could always be changed.\n" +
            "\n" +
            "We chose you to perform this task because we determined you were competent enough. This is not something we said as a compliment. It was simply our evaluation of you. If our evaluation ultimately proves to be incorrect, then that is a reflection on you, not on us.\n" +
            "\n" +
            "As for what this substance does to the body, and what it requires from the body – those things are both very real. None of us will provide you information regarding how to anticipate what might occur after you use this substance. You should go in without anticipation.\n" +
            "\n" +
            "Your conduct subsequent to what you observed today will be governed by the consequences of what you saw today. This is not merely ceremonial.\n" +
            "\n" +
            "Do not confuse what we gave you with generosity. We gave you absolutely nothing. We showed you a doorway. What occurs beyond the doorway is strictly up to you."
        ));

        Register(new ScrollEntry("ashveil-2", "Record of Internal Affairs II", "The Ashveil Brotherhood — author unknown", "Fire",
            "…he used more than he was instructed to. This was normal.\n" +
            "\n" +
            "They all do.\n" +
            "\n" +
            "This is where things get interesting. After this, the candle next to him burned out then lit again. Neither one of us touched the candle. The air did not budge. We documented that & said nothing – by explaining what they see you can destroy their ability to learn; either let them figure it out for themselves or provide no answers. Both options tell you something.\n" +
            "\n" +
            "He didn't notice. A lot of people don't the first time around. They're so caught up in the internal events of what's going on to pay attention to external changes. At some point, that internal/external balance changes. Some people experience that change very quickly. Others never make that transition.\n" +
            "\n" +
            "In less than a month we'll determine which type he is.\n" +
            "\n" +
            "He hasn't talked about it since. Good. People who talk too much about their experiences the first time usually aren't worth your investment. Using words makes something smaller. There is still much that needs to be learned before he can even begin to understand what his experience was.\n" +
            "\n" +
            "Eventually, he'll realize that more isn't better. It's a lesson most people will figure out sooner rather than later. We will not accelerate it.\n" +
            "\n" +
            "He is being watched."
        ));

        Register(new ScrollEntry("ashveil-3", "Record of Internal Affairs III", "The Ashveil Brotherhood — author not provided", "Fire",
            "…He had spoken outside.\n" +
            "\n" +
            "He was discovered in the early afternoon on the fifth day. The case was closed by nightfall. His family by the next morning.\n" +
            "\n" +
            "We are thorough.\n" +
            "\n" +
            "That he said was just a fraction of what he knew. That's why we recorded this; to detail how much of a break-through there was (not to honor those who created the breach). None of that is warranted.\n" +
            "\n" +
            "The area has been secured. The processes remain ours. Whatsoever he communicated to us was insufficient for it to be recreated by anyone else that listened. We were able to verify this firsthand and directly from him.\n" +
            "\n" +
            "As a principle guiding decisions of this nature, knowledge dies not when the individual carrying it dies — it passes. To his family. To anyone that was close enough to listen carefully. As such, we don't take chances here. Never have. Precision is not cruel.\n" +
            "\n" +
            "His family is done. The breach is done. The case is closed.\n" +
            "\n" +
            "A record of no-name(s) will exist. This will be filed as well as not referred back to again, except if this happens again; it won't, since every individual standing where he did will see this record prior to making an application twice.\n" +
            "\n" +
            "They can form their own conclusion. This is our way."
        ));

        Register(new ScrollEntry("ashveil-4", "Record of Internal Affairs IV", "The Ashveil Brotherhood — founding document, author withheld", "Fire",
            "…this isn't a philosophy — it's a conclusion.\n" +
            "\n" +
            "Fire is not another pathway. Fire is the only way we've ever created anything worth preserving. Everything else is just noise. Every other person that has decided it was easier to find some 'wisdom' instead of doing the work; is noise.\n" +
            "\n" +
            "We don't co-exist. We don't compromise. We won't give courtesy to anyone who chose weakness. They aren't equal. The world doesn't need their approval to reflect this fact — only our willingness to act on it.\n" +
            "\n" +
            "People carrying fire but refusing to use it fully are the worst of all. They were gifted something and chose to waste it. I would rather see someone who never had access to fire as opposed to someone that did — but refused to utilize it. People who refuse to use fire will be dealt with as such.\n" +
            "\n" +
            "There is no middle ground in this issue. Either you are with us or you are in the way. Being in the way has consequences that this document doesn't have to enumerate. Every person that is reading this already knows what those consequences are. That's why they're reading it.\n" +
            "\n" +
            "We're not building something. We're clearing something. What happens after we clear is not our concern. Our concern is clearing.\n" +
            "\n" +
            "It doesn't need to be understood. It needs to be done."
        ));

        // ── Fire: Field Manual ────────────────────────────────────────────────
        Register(new ScrollEntry("fieldmanual-1", "Field Manual — Engagement I", "Unit designation redacted — pages missing", "Fire",
            "…control before output. The first thing and the only thing until your commanding officer tells you otherwise.\n" +
            "\n" +
            "You are here to burn nothing in front of you. Anyone with a torch can do that. You are here because you can do something a torch cannot — you can choose. How much. Where. For how long. These are not instincts yet. They will be. Until then you treat every engagement as if it were controlled exercise. Not release.\n" +
            "\n" +
            "The soldiers who wash out in the first month are not those who lack power. Those who lack power would have already washed out. They are the ones who cannot hold it. Who feel it. And immediately push it outside their body because holding it is uncomfortable. That discomfort is the training. Sit in it.\n" +
            "\n" +
            "In formation, you act independently. You wait for the call. When you get it you execute. And stop when you're told to. Not when you feel like you're done. When you're told.\n" +
            "\n" +
            "A soldier who cannot stop is not a soldier. He's a problem for the enemy to deal with — and we will deal with him ourselves.\n" +
            "\n" +
            "You will spar with non-mage soldiers regularly. This is intentional. You will learn to calibrate — enough to be effective, not so much that you destroy what's standing next to you. Your unit is not your target. Remember this when things get hot and thinking gets tougher."
        ));

        Register(new ScrollEntry("fieldmanual-2", "Field Manual — Field Operations", "Unit designation redacted — pages missing", "Fire",
            "…a fire mage who can't feed himself in the field is a liability. The reason this area exists is because we've had liabilities.\n" +
            "\n" +
            "You don't need a fire pit. You don't need flint. You don't need dry wood, good weather, or anything else your unit has been spending their time searching for. That is an advantage and you're going to be using it.\n" +
            "\n" +
            "There is nothing to teach about cooking with your hands. There is little to instruct and lots to permit. Most recruits will figure out how to cook before reaching this point. What prevents them is whether or not they are permitted to.\n" +
            "\n" +
            "They should. Cooking is faster than hunting, fishing, and gathering. It leaves no evidence of where you were, giving your unit an option that the enemy cannot anticipate or counter.\n" +
            "\n" +
            "The only issue when it comes to heat is managing it. You are generally not just cooking for yourself. Be aware of the temperature limits for your body and the temperature requirements for the food. Those limits are different.\n" +
            "\n" +
            "Other than food, you are the unit's fire resource in every condition. Weather doesn't prevent you from doing your job; rain won't stop you; wind won't stop you. This isn't ancillary to your position — it will become central to it during prolonged periods in the field. A unit that stays warm at night and can communicate over long distances without equipment has options that units without those capabilities do not.\n" +
            "\n" +
            "Use what you are."
        ));

        // ── Air: R. Aldenmoor ─────────────────────────────────────────────────
        Register(new ScrollEntry("aldenmoor-1", "Field Notes by R. Aldenmoor I", "R. Aldenmoor — uncertain date", "Air",
            "…the camp, or what passed for one, emerged suddenly out of the fog. Shelters of thin, pale driftwood and stretched animal skin clung to the top of the ridge; as if they grew there naturally, just as the lichens did on the rocks under my feet. I had climbed for three days. Altitude had my lungs burning. They seemed to have no similar discomfort.\n" +
            "\n" +
            "These people were watching me when I arrived. Not with suspicion or even interest — rather with a patient wait for the weather to roll through a valley. I estimated about thirty people. However, due to the fog, I couldn't be sure.\n" +
            "\n" +
            "What caught my attention wasn't their lack of speech or movement. Rather, it was an unfamiliar scent — pungent, somewhat green, with a faintly sharp taste — much like freshly crushed pine needles left out in open space.\n" +
            "\n" +
            "It took me two evenings to identify the source of the odor. They burn a flower. It's a tiny, pale flower (nearly white) that grows within the crevices of rocky areas at high altitudes — and nowhere else. Every morning, prior to sunrise, the oldest member of their group places a bundle of the dried flowers into a small clay dish of smoldering embers. The smoke doesn't ascend. Instead it spreads along the ground in a deliberate manner — as if it knew exactly where to travel.\n" +
            "\n" +
            "For four consecutive mornings, I viewed their fire burning ceremony. On the fifth day, the oldest person involved in this ritual handed me the container containing the embers. I refused. I'm not really sure why."
        ));

        Register(new ScrollEntry("aldenmoor-2", "Field Notes by R. Aldenmoor II", "R. Aldenmoor — uncertain date", "Air",
            "…my time with these people has been fourteen days now. The fog has remained in place the entire time.\n" +
            "\n" +
            "It was during this time that I began recording individual characteristics about those I've observed — not by names (they didn't offer me any), but based on their physical attributes. The tall man with the scarred arm. The woman who sits farthest away from the fire. The young boy who carries water into camp twice per day without ever being told to.\n" +
            "\n" +
            "This practice arose out of an observation I couldn't immediately describe. Those participants in the morning ritual seemed different to me. They were no faster in my ability to measure; however, there existed some other quality I found difficult to accurately describe using my own writing tools.\n" +
            "\n" +
            "The tall man traversed the large amount of loose shale which took me two days to navigate. He neither looked up nor slowed. The shale beneath his feet did not react to him like it reacted to me.\n" +
            "\n" +
            "By the sixteenth day, a water vessel fell from the hands of a young woman while she was standing near the ridgeline. She caught it. What made this event noteworthy for me is that I had previously recorded the noise it made when it struck the earth. Upon reviewing my notes after the fact, I realized that I wrote about the noise of the vessel striking the earth before I even documented that she had caught it.\n" +
            "\n" +
            "Wind at this elevation is very unpredictable and can quickly change its path without a pattern, many times mid-stride. I have lost my balance and fallen twice due to the unpredictability of this wind. These people don't appear to lose their footing. In fact, they seem to move within the wind instead of against it, almost as if they know where the next gust will come from.\n" +
            "\n" +
            "The woman who is located furthest away from the fire never participates in the morning ritual. I watched her traverse the same area of loose shale. She proceeded with care similar to how most would proceed.\n" +
            "\n" +
            "I am making no assumptions about anything. I simply document what I observe."
        ));

        Register(new ScrollEntry("aldenmoor-3", "Field Notes by R. Aldenmoor III", "R. Aldenmoor — undated", "Air",
            "…the last day of my time here. In total, twenty-three days.\n" +
            "\n" +
            "We followed the routine. First, the oldest. We ground the dried flower using the same method we have used for years. Between the two flat stones, a very fine pale powder formed. The powder was then put into the small stem of the stone vessel, which was sealed on the bottom. Enough water was added so that some of the water reflected the light. There was no visible plume of smoke; instead, it seemed to travel over the surface of the stone before disappearing.\n" +
            "\n" +
            "My equipment was being prepared while this was happening. It didn't seem right to be watching today, but I couldn't tell you why. I don't tend to get sentimental. I documented the feeling and continued with my work.\n" +
            "\n" +
            "Before I went down, I checked my botanical collection. I had eleven pressed samples of the flower, each powdered to a very pale white. I also had one sample preserved in wax; it is currently intact (as of this writing), although the petals are beginning to lose their translucent quality. I have no idea how much longer it will remain.\n" +
            "\n" +
            "I have reviewed my entire set of field notes for this project. All of my data has remained consistent throughout. However, I am now faced with a challenge related to vocabulary. None of the terms available to describe what I observed — speed, agility, awareness, or reflex — can adequately capture the essence of what I saw. Each term implies a different level of performance; what I actually witnessed was an entirely different type of performance altogether."
        ));

        // ── Air: E. Voss ──────────────────────────────────────────────────────
        Register(new ScrollEntry("voss-1", "Winter Residency Notes I", "E. Voss — year unspecified", "Air",
            "…My third winter with them. The cold here is different than the cold you would find in lower elevations. Cold like this has a presence.\n" +
            "\n" +
            "By now I had given up trying to describe what I was seeing in terms that were conventional. Not from losing hope — but from discipline. Any attempt to explain something as complex as weather requires some sort of framework. At this time I had no such framework.\n" +
            "\n" +
            "On the forty-second day of that winter, one of the younger men — the man I would eventually begin referring to as \"Scar\" due to a scar he had on his left jaw — began to cross the small stone bridge which connected the northern and eastern ridges. I had crossed this very same bridge earlier in the day. The width of the bridge at its smallest area is approximately four feet. The winds were extremely strong on this particular day.\n" +
            "\n" +
            "He didn't stretch out his arms for balance. He didn't slow down. Approximately midway through the bridge, a gust of wind hit him horizontally — much like the horizontal gusts that had knocked me to one knee two days before. He leaned into it. Not away from it. But into it. And he kept walking.\n" +
            "\n" +
            "That night, I entered in my journal: the wind didn't move him. Then I erased that entry. Then I re-entered it.\n" +
            "\n" +
            "What I want to convey about this experience, and how difficult it has been for me to put into words is: the interaction between his body and the air surrounding him appeared to be nothing like the interaction between my body and the air surrounding mine. As if the wind recognized him. Or if there was a deal.\n" +
            "\n" +
            "I am fully cognizant of how these words will read. Nonetheless, I still choose to write them."
        ));

        Register(new ScrollEntry("voss-2", "Winter Residency Notes II", "E.V. — undated", "Air",
            "…the occurrence took place with no warning (as do most events).\n" +
            "\n" +
            "We were walking along the top of the upper eastern path — me, Scar, and two other people whose names I could not remember yet. The path becomes much narrower where we were walking. I always walk right next to the rock wall when I walk through here. The fall is quite deep on the other side of the path.\n" +
            "\n" +
            "The younger one of the unknown — possibly a young girl — stepped off into space. I heard the noise of her foot sliding off onto the loose rocks before I saw anything. And then nothing under it.\n" +
            "\n" +
            "She went over the edge of the cliff.\n" +
            "\n" +
            "I didn't move. I'm not particularly proud of that. There wasn't time to respond by moving anyway.\n" +
            "\n" +
            "Now follows how I think about what happened after she fell. I've tried to reconstruct all of this out of my mind many times now. Each time I try to reconstruct it, the results are always the same and produce a mix of comfort and discomfort within me.\n" +
            "\n" +
            "She dropped like anyone else would. This is an important point to make clear — the first part of the fall was typical. What was not normal was whatever produced an upward thrust (not her) approximately two-thirds of the way down; perhaps less than one second before she hit the ground. Her hair and clothes shot up in reaction to whatever was producing the upward thrust. The falling motion did not slow down gradually; it stopped. Then she hit the ground — smoothly, feet-first, as if she had chosen to jump down from a reasonable height.\n" +
            "\n" +
            "Then she got up. She looked back at us.\n" +
            "\n" +
            "Scar had turned his head away from looking at her and was again walking back up the trail.\n" +
            "\n" +
            "I spent a considerable amount of time standing at the edge after they left. The air blowing from below was normal. I checked it with my hand. Normal in all respects."
        ));

        Register(new ScrollEntry("voss-3", "Winter Residency Notes III", "E. Voss — year unspecified", "Air",
            "…I had never witnessed them arguing with each other. Over the course of three winters, I had noticed disagreements among the group. However, these disagreements were of a silent nature; they would be expressed by physical distance from one another and the purposeful placement of their bodies when sitting around a fire. This was the first instance where this was clearly evident.\n" +
            "\n" +
            "I do not know why they were arguing. While I have not yet fully understood the language spoken at times by those at camp, neither of them spoke in slow or simple terms in order for me to understand.\n" +
            "\n" +
            "They were Scar and the Older Man whom I have referred to as the \"Mender.\" At approximately ten feet apart on the flat rock above the camp, the two men argued. If an argument occurred — which may be an inaccurate term — it seemed to have been developing over several hours prior to my becoming aware of it.\n" +
            "\n" +
            "First, I became aware of the wind. As the wind blew around Scar — particularly, his hair, clothing, and loose strings attached to his pack — they appeared to move against the primary movement of the wind. Not greatly. Not noticeably incorrect for anyone else present and paying attention. However, I had paid great attention to such movements for three winters.\n" +
            "\n" +
            "The louder Scar's voice grew, the greater the extent of this aberration became. For example, when Scar's left foot moved slightly closer to the ground due to the shifting of a small pebble next to it, there was nothing physically touching the pebble.\n" +
            "\n" +
            "The Mender then said something brief and then turned and went back toward camp.\n" +
            "\n" +
            "Within a very few minutes, the wind stopped blowing as it had and Scar remained standing alone on top of the flat rock. Eventually, he sat down. I did not go to him.\n" +
            "\n" +
            "Later that night I revisited all seventeen entries of my notes detailing how various phenomena are affected during times when the people in camp appear emotionally upset. I previously determined that none of these occurrences could be attributed to anything but coincidence."
        ));

        Register(new ScrollEntry("voss-4", "Winter Residency Notes IV", "E. Voss — year unspecified", "Air",
            "…my fifth winter. My intention was not to remain here so long.\n" +
            "\n" +
            "Over the past few years, I have put a great deal of effort into developing a theoretical framework that could explain what I see. Unfortunately, it's been much harder than I expected. There is no shortage of data; I've got far too many notes to organize. What is missing is adequate language. The words we use today were created for a different purpose.\n" +
            "\n" +
            "With absolute certainty, I know one thing. These individuals have an association between themselves and their surrounding atmosphere that does not follow any of the natural principles that I learned to utilize. This is not a metaphor. This is not because of superior physical condition (although they do possess superior physical conditioning).\n" +
            "\n" +
            "Whatever this is, it exists between an individual and his or her surroundings. Whatever this is, it appears to be capable of being cultivated. This appears to be capable of being lost. The woman sitting furthest away from the fire has not taken part in the morning activity for nearly two seasons. I am aware of the differences.\n" +
            "\n" +
            "In addition, I now believe that what I witnessed in Scar two winters ago during the confrontation was not an isolated event. Rather, I believe it represents a glimpse of some other process which is operating continuously but at a lower level — some other process which is only observable when the correct conditions prevail."
        ));
    }

    private static void Register(ScrollEntry e) => _scrolls[e.Id] = e;

    public static ScrollEntry? Get(string id) => _scrolls.TryGetValue(id, out var e) ? e : null;

    public static IEnumerable<ScrollEntry> All => _scrolls.Values;

    public static IEnumerable<ScrollEntry> ByElement(string element)
    {
        foreach (var e in _scrolls.Values)
            if (e.Element == element) yield return e;
    }
}
