.pragma library

/*
 * 安全小问答题库（离线）：40 道题，覆盖交通 / 防拐 / 防火 / 用电 / 溺水 /
 * 饮食 / 灾害 / 网络 / 急救 / 自我保护。
 *
 * 与 SafetyData.js 是两套东西：那边是「读的贴士」，这里是「答的题」。
 * 两边的分类名刻意保持一致，孩子读完某个主题再做题能对上号。
 *
 * 选项顺序在出题时打乱，`correct` 记录的是**本数组里**正确项的下标，
 * 打乱后靠它重新定位。文案与三端共用，不要在这里单独改。
 */

var QUESTIONS = [
    {
        emoji: "🚦", category: "交通安全", question: "过马路时，应该怎么做才安全？",
        options: ["红灯时直接跑过去", "走斑马线，绿灯亮了再看车再过", "在车流中穿过去"],
        correct: 1, explain: "要走斑马线（人行横道），绿灯亮、左右看没车再过马路，绝不闯红灯。"
    },
    {
        emoji: "🚸", category: "交通安全", question: "小朋友可以一个人到马路上玩耍吗？",
        options: ["可以，马路很宽", "不可以，马路上车多很危险", "只在晚上玩"],
        correct: 1, explain: "马路是车辆行驶的地方，小朋友要在大人陪同下走人行道，不能在马路上玩耍。"
    },
    {
        emoji: "🚗", category: "交通安全", question: "坐小汽车时，下面哪种做法最安全？",
        options: ["坐在后排并系好安全带", "坐在副驾驶抱着玩", "把头和手伸出窗外"],
        correct: 0, explain: "小朋友要坐后排安全座椅或系好安全带，头手不能伸出窗外，更不能坐副驾驶。"
    },
    {
        emoji: "🚌", category: "交通安全", question: "在公交车上，下列哪种行为不对？",
        options: ["站稳扶好扶手", "把头伸出车窗外", "听大人安排坐好"],
        correct: 1, explain: "乘车时不能把头、手伸出窗外，要站稳扶好，听从大人安排。"
    },
    {
        emoji: "🍬", category: "防骗防拐", question: "有陌生人说「我是你妈妈的朋友，带你去吃糖」，你该怎么做？",
        options: ["马上跟他走", "不跟陌生人走，先问爸爸妈妈", "自己偷偷去"],
        correct: 1, explain: "绝对不跟陌生人走！就算他说认识爸爸妈妈，也要先和大人确认，不吃陌生人的东西。"
    },
    {
        emoji: "🏠", category: "防骗防拐", question: "一个人在家时，有人敲门说是「修水管的」，应该？",
        options: ["立刻开门让他进来", "不开门，先打电话问爸爸妈妈", "让他自己进来修"],
        correct: 1, explain: "独自在家不给陌生人开门。先给爸爸妈妈打电话确认，必要时联系物业或邻居。"
    },
    {
        emoji: "📱", category: "防骗防拐", question: "如果和爸爸妈妈走散了，最好的办法是？",
        options: ["大哭着乱跑", "找穿制服的警察/保安，或待在原地等", "跟陌生人去「找妈妈」"],
        correct: 1, explain: "走散时不要乱跑，待在原地或找穿制服的叔叔阿姨帮忙，记住爸爸妈妈电话。"
    },
    {
        emoji: "🚗", category: "防骗防拐", question: "校门口有人说「你妈妈出事了，让我来接你」，你应该？",
        options: ["赶紧跟他走", "先给爸爸妈妈打电话确认，打不通就找老师或保安", "自己跑回家"],
        correct: 1, explain: "不管对方说得多急，都要先打电话确认。打不通就回学校找老师、找门卫，绝对不能上陌生人的车。"
    },
    {
        emoji: "🧭", category: "防骗防拐", question: "陌生人问路，还让你带他过去，你应该？",
        options: ["带他一起走过去", "只告诉他怎么走，自己不走开", "上他的车帮他带路"],
        correct: 1, explain: "指路可以，带路不行。大人不需要小孩帮忙带路，真需要帮助的人会去找其他大人。"
    },
    {
        emoji: "🙅", category: "防骗防拐", question: "有人拉住你的胳膊要带你走，最好的做法是？",
        options: ["安静地跟他走", "大声喊「我不认识你」，抱住路边的树或栏杆", "先跟他走，再找机会跑"],
        correct: 1, explain: "大声喊叫、抱住固定物，会让周围的人注意到你。坏人最怕被看见，一喊他往往就松手了。"
    },
    {
        emoji: "🏪", category: "防骗防拐", question: "在超市和妈妈走散了，你应该？",
        options: ["跑到超市外面找", "走到收银台，请店员帮忙广播找妈妈", "跟一个陌生人去找妈妈"],
        correct: 1, explain: "超市、商场里都有工作人员。站在收银台这种人多、有摄像头的地方等，比出去找安全得多。"
    },
    {
        emoji: "🎁", category: "防骗防拐", question: "有人对你说「这件事别告诉你爸妈」，你该怎么做？",
        options: ["答应他，保密", "回家就告诉爸爸妈妈", "只告诉同学"],
        correct: 1, explain: "要你瞒着爸妈的事，本身就有问题。真正对你好的人，不会让你对爸妈保密。"
    },
    {
        emoji: "🚫", category: "防骗防拐", question: "网上认识的「朋友」约你单独见面，你应该？",
        options: ["偷偷去见一面", "不去，并告诉爸爸妈妈", "让他到学校门口等"],
        correct: 1, explain: "网上的人可能是任何年纪、任何身份。不能单独见网友，也不能把学校和住址告诉他。"
    },
    {
        emoji: "🔥", category: "防火安全", question: "看到插座冒火花或有焦味，应该？",
        options: ["用水泼", "马上告诉大人，不自己去碰", "用湿手去拔插头"],
        correct: 1, explain: "电器着火不能用水！要立刻告诉大人或拨打火警，自己不要去碰带电的东西。"
    },
    {
        emoji: "🚭", category: "防火安全", question: "可以在床上或窗帘边玩打火机、火柴吗？",
        options: ["可以，很好玩", "绝对不可以，很容易着火", "只玩一下下"],
        correct: 1, explain: "火很危险，打火机、火柴不是玩具，绝不能玩，更不能靠近床和窗帘。"
    },
    {
        emoji: "🧯", category: "防火安全", question: "如果身上衣服着火了，应该？",
        options: ["站着跑", "就地打滚压灭火苗", "用水浇头"],
        correct: 1, explain: "衣服着火要「停、躺、滚」，就地打滚压灭火苗，不要站着跑让火更大。"
    },
    {
        emoji: "🔌", category: "用电安全", question: "下面哪种做法不安全？",
        options: ["用干手碰电器", "用湿手去插拔插头", "让大人检查旧电线"],
        correct: 1, explain: "水会导电，湿手碰插座或电器容易触电，一定要用干手，并由大人操作。"
    },
    {
        emoji: "💡", category: "用电安全", question: "发现电线破损露出铜丝，应该？",
        options: ["自己用手去包", "告诉大人，不去碰它", "当作没看见"],
        correct: 1, explain: "破损电线很危险，不能自己碰，要马上告诉大人请专业人员修理。"
    },
    {
        emoji: "🤝", category: "保护自己", question: "陌生人对你说「这是我们的秘密，别告诉爸妈」，你该？",
        options: ["答应保密", "马上告诉爸爸妈妈", "只对好朋友说"],
        correct: 1, explain: "真正的秘密不是要你瞒着爸妈的。如果大人让你保密，一定要告诉爸爸妈妈！"
    },
    {
        emoji: "🙅", category: "保护自己", question: "别人让你感到不舒服的触摸，你应该？",
        options: ["忍着不说", "大声说「不」，并告诉信任的大人", "照他说的做"],
        correct: 1, explain: "身体是属于自己的！遇到不舒服的接触要勇敢说「不」，并马上告诉信任的大人。"
    },
    {
        emoji: "🩲", category: "保护自己", question: "背心短裤盖住的地方，谁可以随便看、随便摸？",
        options: ["谁都不可以", "医生随时都可以", "熟悉的亲戚可以"],
        correct: 0, explain: "隐私部位谁都不能随便看、随便摸。医生检查身体时，也一定要有爸爸妈妈在旁边。"
    },
    {
        emoji: "🤝", category: "保护自己", question: "熟悉的叔叔、邻居或老师让你不舒服，你可以拒绝吗？",
        options: ["不可以，要听话", "可以，礼貌没有安全重要", "只能小声说不"],
        correct: 1, explain: "做坏事的常常是熟人。不管是谁，让你不舒服的接触都可以拒绝，然后告诉爸妈。"
    },
    {
        emoji: "🎁", category: "保护自己", question: "有人给你礼物，让你脱衣服拍照，你会？",
        options: ["收下礼物照做", "拒绝、马上离开，并告诉爸妈", "先拍一张试试"],
        correct: 1, explain: "给你好处再提要求的，一定是坏人。马上离开，把这件事告诉爸妈，不用觉得羞。"
    },
    {
        emoji: "💔", category: "保护自己", question: "如果有人碰了你的隐私部位，这件事是谁的错？",
        options: ["是我自己的错", "不是我的错，要马上告诉爸妈", "不能说出去，会丢人"],
        correct: 1, explain: "错的永远是做这件事的大人。你没有做错任何事，告诉爸妈，他们会保护你。"
    },
    {
        emoji: "👨‍⚕️", category: "保护自己", question: "医生要检查身体，但爸爸妈妈不在旁边，你该？",
        options: ["自己脱衣服配合", "先说不，等爸妈来了再检查", "马上跑掉不检查"],
        correct: 1, explain: "医生检查身体时，爸妈要在场。没有大人在，你可以说「等我妈妈来」。"
    },
    {
        emoji: "🌊", category: "防溺水", question: "想去玩水时，下面哪种做法对？",
        options: ["一个人偷偷去河边", "在大人陪同下去正规泳池", "在雨后水坑里玩"],
        correct: 1, explain: "玩水必须有大人陪同，去正规泳池；不能独自去河边、池塘、水库，很危险。"
    },
    {
        emoji: "🆘", category: "防溺水", question: "看到小伙伴掉进水里，你应该？",
        options: ["自己跳下去救", "大声喊「救命」并找大人帮忙", "走开不管"],
        correct: 1, explain: "小朋友不能下水救人！要大声呼救、找附近的大人，或拨打急救电话。"
    },
    {
        emoji: "🍎", category: "饮食安全", question: "吃水果前，最好先？",
        options: ["直接咬", "洗干净再吃", "放地上滚一下"],
        correct: 1, explain: "水果蔬菜要洗干净再吃，能洗掉脏东西和农药，保护肚子不生病。"
    },
    {
        emoji: "🥫", category: "饮食安全", question: "下面哪种东西不能吃？",
        options: ["妈妈做的热饭", "过期的零食", "洗好的苹果"],
        correct: 1, explain: "过期、变质的食物会让人拉肚子甚至中毒，看到过期就别吃，告诉大人。"
    },
    {
        emoji: "💊", category: "饮食安全", question: "药片和糖果长得很像，应该？",
        options: ["自己拿来尝", "分不清时问大人", "当糖吃"],
        correct: 1, explain: "药不是糖！不认识的东西不能放进嘴里，分不清时一定要问大人。"
    },
    {
        emoji: "🌏", category: "灾害安全", question: "发生地震时，在室内应该？",
        options: ["坐电梯下楼", "躲到结实的桌子下，护住头", "跑到窗边看热闹"],
        correct: 1, explain: "地震时别乘电梯，要躲到结实桌子下护住头，等摇晃停了再听大人指挥撤离。"
    },
    {
        emoji: "⛈️", category: "灾害安全", question: "打雷闪电时，下面哪种做法安全？",
        options: ["在大树下躲雨", "到室内并远离窗户", "站在空地最高处"],
        correct: 1, explain: "雷雨天要到室内，远离大树、电线和窗户，不在户外高处停留。"
    },
    {
        emoji: "💻", category: "网络安全", question: "在网上，下面哪种信息不能告诉陌生人？",
        options: ["你喜欢的颜色", "姓名、学校和家庭住址", "你喜欢的动画"],
        correct: 1, explain: "姓名、学校、家庭住址、密码这些隐私不能告诉网友，保护好自己的信息。"
    },
    {
        emoji: "🎁", category: "网络安全", question: "网友说「送你游戏礼包，给我密码」，你该？",
        options: ["把密码发给他", "不给他，并告诉爸爸妈妈", "先试试"],
        correct: 1, explain: "天上不会掉礼包！任何要你密码、验证码的人都是骗子，马上告诉大人。"
    },
    {
        emoji: "💬", category: "网络安全", question: "网上认识的人聊了几天就说「我们最懂彼此」，还问你家住哪，你该？",
        options: ["如实告诉他", "不回答，告诉爸妈并拉黑", "先聊着看看"],
        correct: 1, explain: "真心交朋友不会急着打听隐私。问住址、学校、几点一个人在家的，都要警惕。"
    },
    {
        emoji: "🤐", category: "网络安全", question: "网友说「这件事别告诉爸妈，是我们俩的秘密」，说明？",
        options: ["他很信任我", "他很可能有坏心思，要马上告诉爸妈", "这是礼貌"],
        correct: 1, explain: "正常的交往不需要瞒着爸妈。让你保密的人，往往是在为做坏事做准备。"
    },
    {
        emoji: "📸", category: "网络安全", question: "网上有人威胁你「不发照片就把聊天记录发出去」，你该？",
        options: ["按他说的发照片", "截图留证据，告诉爸妈，必要时报警", "把他删了就没事"],
        correct: 1, explain: "威胁你的人才是做错事的人。先截图留证据，再告诉爸妈，不要妥协、也不要自己删掉。"
    },
    {
        emoji: "🚫", category: "网络安全", question: "网上认识的「姐姐」约你单独见面，你该？",
        options: ["偷偷去见她", "不去；要去也要爸妈陪着，在人多的地方", "叫上同学一起去就行"],
        correct: 1, explain: "网友不能单独见面。真要去，必须爸妈陪同、白天、在人多的地方。"
    },
    {
        emoji: "📞", category: "急救电话", question: "着火了应该打哪个电话？",
        options: ["110", "119", "120"],
        correct: 1, explain: "火警是 119，急救是 120，报警是 110。记住它们，危险时才能求救。"
    },
    {
        emoji: "🚑", category: "急救电话", question: "有人突然晕倒、受伤很严重，应该打？",
        options: ["120", "10086", "12315"],
        correct: 0, explain: "急需医疗救助打 120；记住家庭地址和爸爸妈妈电话，方便说清楚位置。"
    }
];

/// 一轮的题量：40 题里抽 10 道，三四分钟能做完，不至于让孩子失去耐心。
var ROUND_SIZE = 10;

/**
 * 抽一轮题。
 *
 * 两个随机：
 *   - 抽哪 10 道（topic 打乱后取前 N）；
 *   - 每道题的选项顺序也打乱，避免孩子记住「对的总是第二个」。
 *
 * 返回的对象是拷贝，correct 已经换成新选项数组里的下标。
 */
function buildRound() {
    var pool = [];
    for (var i = 0; i < QUESTIONS.length; ++i) {
        pool.push(QUESTIONS[i]);
    }

    // Fisher–Yates 洗牌
    for (var k = pool.length - 1; k > 0; --k) {
        var j = Math.floor(Math.random() * (k + 1));
        var tmp = pool[k];
        pool[k] = pool[j];
        pool[j] = tmp;
    }

    var round = [];
    var count = Math.min(ROUND_SIZE, pool.length);
    for (var n = 0; n < count; ++n) {
        round.push(shuffleOptions(pool[n]));
    }
    return round;
}

function shuffleOptions(question) {
    var order = [];
    for (var i = 0; i < question.options.length; ++i) {
        order.push(i);
    }
    for (var k = order.length - 1; k > 0; --k) {
        var j = Math.floor(Math.random() * (k + 1));
        var tmp = order[k];
        order[k] = order[j];
        order[j] = tmp;
    }

    var options = [];
    var correct = 0;
    for (var n = 0; n < order.length; ++n) {
        options.push(question.options[order[n]]);
        if (order[n] === question.correct) {
            correct = n;
        }
    }

    return {
        emoji: question.emoji,
        category: question.category,
        question: question.question,
        options: options,
        correct: correct,
        explain: question.explain
    };
}

/// 按对题数给一句评语，不做排行榜——这个年龄不需要。
function verdict(score, total) {
    if (score === total) {
        return "全部答对！这些安全知识你都记住了 🎉";
    }
    if (score * 2 >= total) {
        return "很不错，大部分危险你都能认出来 👍";
    }
    if (score > 0) {
        return "有进步空间，回安全学堂再读一遍吧 💪";
    }
    return "别急，先读一遍贴士，再来试试 🌱";
}
