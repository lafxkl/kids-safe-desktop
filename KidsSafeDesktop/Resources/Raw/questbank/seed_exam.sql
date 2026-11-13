-- 模拟考试题库：3-9 年级 × 语文 / 数学 / 英语，每年级每科 1 套期末模拟卷，共 21 套 252 题
-- 每套 12 题（单选 + 填空 + 判断），交卷后按百分制打分；答案与解析需与题目同批维护
-- 填空题的多种等价写法用 | 分隔；判断题只存「对」或「错」
-- 题干中的单引号统一写成 ''（SQL 转义），中文引号用「」
DELETE FROM exam_question;
DELETE FROM exam_paper;

-- ============ 三年级语文 ============
INSERT INTO exam_paper(id,code,grade,subject,title,term,duration_min,note) VALUES
(1,'g3-chinese',3,'chinese','三年级上册语文期末模拟卷','上册',40,'字音字形 · 古诗积累 · 课文理解');

INSERT INTO exam_question(id,paper_id,ordinal,kind,stem,options,answer,explain) VALUES
(1,1,1,'choice','下列词语中，「铺满」的「铺」读音正确的一项是（　）。','pū|pù|bū|pú','A','「铺满」是铺开、摊开的意思，读 pū。'),
(2,1,2,'choice','「秋天的雨，是一把钥匙。」这句话使用的修辞手法是（　）。','比喻|拟人|夸张|排比','A','把秋雨比作钥匙，属于比喻。'),
(3,1,3,'choice','《山行》中「停车坐爱枫林晚」的「坐」意思是（　）。','坐下|因为|座位|乘坐','B','「坐」在这里是「因为」的意思。'),
(4,1,4,'choice','下列词语没有错别字的一项是（　）。','穿带|打扮|荒张|保贵','B','A 应为「穿戴」，C 应为「慌张」，D 应为「宝贵」。'),
(5,1,5,'choice','「橘子、柿子你挤我碰，争着要人们去摘呢！」这句话把橘子、柿子当作（　）来写。','人|物|动物|植物','A','「你挤我碰」「争着」都是人的动作，属于拟人。'),
(6,1,6,'fill','看拼音写词语：huāng yě（　　）','','荒野','「荒野」指荒凉的野外。'),
(7,1,7,'fill','《赠刘景文》中「荷尽已无擎雨盖，（　　）。」','','菊残犹有傲霜枝','苏轼《赠刘景文》中的名句。'),
(8,1,8,'fill','「一年好景君须记，最是橙黄橘绿时。」这两句诗的作者是（　　）。','','苏轼','《赠刘景文》是宋代苏轼写给好友刘景文的诗。'),
(9,1,9,'fill','写出「美丽」的反义词：（　　）。','','丑陋|难看','「美丽」与「丑陋」意思相反。'),
(10,1,10,'fill','《铺满金色巴掌的水泥道》中，「金色巴掌」指的是（　　）。','','落叶|梧桐树叶|树叶|法国梧桐的落叶','法国梧桐的落叶像金色的巴掌，铺满水泥道。'),
(11,1,11,'judge','《夜书所见》的作者是宋代诗人叶绍翁。','','对','《夜书所见》为宋代叶绍翁所作。'),
(12,1,12,'judge','「知有儿童挑促织」中的「促织」指的是蝉。','','错','「促织」是蟋蟀的别称。');

-- ============ 三年级数学 ============
INSERT INTO exam_paper(id,code,grade,subject,title,term,duration_min,note) VALUES
(2,'g3-math',3,'math','三年级上册数学期末模拟卷','上册',40,'万以内加减 · 多位数乘一位数 · 周长 · 分数初步');

INSERT INTO exam_question(id,paper_id,ordinal,kind,stem,options,answer,explain) VALUES
(13,2,1,'choice','一枚 1 分硬币的厚度大约是 1（　）。','毫米|厘米|分米|米','A','硬币很薄，量它的厚度用毫米作单位。'),
(14,2,2,'choice','325 + 178 = （　）','493|503|513|403','B','325 + 178 = 503。'),
(15,2,3,'choice','一个正方形的边长是 6 厘米，它的周长是（　）厘米。','12|18|24|36','C','正方形周长 = 边长 × 4 = 6 × 4 = 24 厘米。'),
(16,2,4,'choice','把一个月饼平均分成 8 份，其中的 3 份是（　）。','3/8|8/3|1/3|3/5','A','平均分成 8 份，取其中 3 份就是 3/8。'),
(17,2,5,'fill','360 × 5 = （　）。','','1800','36 × 5 = 180，末尾再补一个 0，得 1800。'),
(18,2,6,'fill','1 分 = （　）秒。','','60','1 分等于 60 秒。'),
(19,2,7,'fill','3 千米 = （　）米。','','3000','1 千米 = 1000 米，3 千米 = 3000 米。'),
(20,2,8,'fill','786 - 249 = （　）。','','537','786 - 249 = 537。'),
(21,2,9,'fill','一个长方形长 8 厘米、宽 5 厘米，它的周长是（　）厘米。','','26','周长 = （8 + 5）× 2 = 26 厘米。'),
(22,2,10,'fill','5/7 里面有（　）个 1/7。','','5','分母是 7，分子是 5，表示 5 个 1/7。'),
(23,2,11,'judge','0 和任何数相乘都得 0。','','对','0 乘任何数结果都是 0。'),
(24,2,12,'judge','一个正方形的周长是 20 厘米，它的边长是 4 厘米。','','错','边长 = 20 ÷ 4 = 5 厘米，不是 4 厘米。');

-- ============ 三年级英语 ============
INSERT INTO exam_paper(id,code,grade,subject,title,term,duration_min,note) VALUES
(3,'g3-english',3,'english','三年级上册英语期末模拟卷','上册',30,'问候语 · 颜色 · 文具 · 数字');

INSERT INTO exam_question(id,paper_id,ordinal,kind,stem,options,answer,explain) VALUES
(25,3,1,'choice','—Hello! —（　）','Hello!|Thank you.|Sorry.|Goodbye.','A','别人说 Hello，用 Hello 回应。'),
(26,3,2,'choice','—What colour is it? —It is（　）.（红色）','red|blue|green|black','A','红色是 red。'),
(27,3,3,'choice','This is（　）pen.（一支）','a|an|two|不填','A','pen 以辅音音素开头，用 a。'),
(28,3,4,'choice','—How old are you? —I am（　）.（9 岁）','nine|nice|fine|ninth','A','九岁是 nine。'),
(29,3,5,'choice','下列单词中表示「书」的是（　）。','book|bag|box|bed','A','book 是书。'),
(30,3,6,'fill','补全单词：p（　）ncil（铅笔）','','e','pencil 是铅笔。'),
(31,3,7,'fill','写出「橡皮」的英文：','','eraser|rubber','eraser 是美式说法，rubber 是英式说法。'),
(32,3,8,'fill','—Good morning! —Good（　）!','','morning','早上问候的固定答语。'),
(33,3,9,'fill','数字 7 的英文是（　）。','','seven','seven。'),
(34,3,10,'fill','颜色「黄色」的英文是（　）。','','yellow','yellow。'),
(35,3,11,'judge','ruler 的意思是「尺子」。','','对','ruler 是尺子。'),
(36,3,12,'judge','字母 b 和 d 是同一个字母。','','错','b 和 d 是两个不同的字母。');

-- ============ 四年级语文 ============
INSERT INTO exam_paper(id,code,grade,subject,title,term,duration_min,note) VALUES
(4,'g4-chinese',4,'chinese','四年级上册语文期末模拟卷','上册',40,'成语 · 古诗积累 · 修辞与病句');

INSERT INTO exam_question(id,paper_id,ordinal,kind,stem,options,answer,explain) VALUES
(37,4,1,'choice','「不识庐山真面目，只缘身在此山中。」出自（　）。','苏轼《题西林壁》|李白《望庐山瀑布》|王维《鹿柴》|杜甫《望岳》','A','这两句出自苏轼的《题西林壁》。'),
(38,4,2,'choice','下列词语书写完全正确的一项是（　）。','风平浪静|峰平浪静|风坪浪静|风平浪净','A','正确写法是「风平浪静」。'),
(39,4,3,'choice','「浪潮越来越近，犹如千万匹白色战马齐头并进。」这句话使用的修辞手法是（　）。','比喻|拟人|夸张|设问','A','把浪潮比作白色战马，属于比喻。'),
(40,4,4,'choice','《暮江吟》中「半江瑟瑟半江红」的「瑟瑟」形容（　）。','风声|碧绿色|寒冷|发抖','B','「瑟瑟」形容江水的碧绿色。'),
(41,4,5,'choice','下列句子没有语病的一项是（　）。','我的家乡是杭州人。|我们要养成讲卫生。|他第一个首先发言。|小明认真地完成了作业。','D','前三句分别有搭配不当、成分残缺、语意重复的问题。'),
(42,4,6,'fill','《题西林壁》中「横看成岭侧成峰，（　　）。」','','远近高低各不同','苏轼《题西林壁》的第二句。'),
(43,4,7,'fill','「嫦娥应悔偷灵药，（　　）。」','','碧海青天夜夜心','出自李商隐《嫦娥》。'),
(44,4,8,'fill','把成语补充完整：人声鼎（　）。','','沸','人声鼎沸，形容人声嘈杂喧闹。'),
(45,4,9,'fill','《观潮》按「潮来前——（　　）——潮头过后」的顺序写钱塘江大潮。','','潮来时|潮来的时候','课文按潮来前、潮来时、潮头过后的顺序写。'),
(46,4,10,'fill','《爬山虎的脚》中，爬山虎的脚长在茎上长（　）的地方。','','叶柄','课文写脚长在茎上长叶柄的地方。'),
(47,4,11,'judge','《蟋蟀的住宅》的作者是法国的法布尔。','','对','法布尔是法国著名昆虫学家。'),
(48,4,12,'judge','盘古、精卫、女娲都是古希腊神话中的人物。','','错','他们都是中国古代神话中的人物。');

-- ============ 四年级数学 ============
INSERT INTO exam_paper(id,code,grade,subject,title,term,duration_min,note) VALUES
(5,'g4-math',4,'math','四年级上册数学期末模拟卷','上册',40,'大数认识 · 角的度量 · 三位数乘两位数 · 除数是两位数');

INSERT INTO exam_question(id,paper_id,ordinal,kind,stem,options,answer,explain) VALUES
(49,5,1,'choice','由 5 个千万、3 个万和 7 个一组成的数是（　）。','50030007|50300007|53000007|50003007','A','千万位是 5、万位是 3、个位是 7，其余数位补 0。'),
(50,5,2,'choice','一个直角是（　）度。','45|60|90|180','C','直角等于 90 度。'),
(51,5,3,'choice','208 × 30 = （　）','6240|624|62400|6040','A','208 × 3 = 624，末尾再补一个 0。'),
(52,5,4,'choice','下面的角中，是钝角的是（　）。','89°|90°|91°|45°','C','大于 90° 而小于 180° 的角叫钝角。'),
(53,5,5,'fill','1 公顷 = （　）平方米。','','10000','1 公顷等于 10000 平方米。'),
(54,5,6,'fill','480 ÷ 40 = （　）。','','12','48 ÷ 4 = 12。'),
(55,5,7,'fill','一个数由 3 个亿、6 个百万和 2 个百组成，这个数写作（　）。','','306000200','亿位 3、百万位 6、百位 2，其余数位是 0。'),
(56,5,8,'fill','从一点引出两条射线所组成的图形叫做（　）。','','角','这是角的定义。'),
(57,5,9,'fill','25 × 4 × 8 = （　）。','','800','25 × 4 = 100，100 × 8 = 800。'),
(58,5,10,'fill','平行四边形的两组对边分别（　）。（填「相等」或「垂直」）','','相等','平行四边形两组对边分别平行且相等。'),
(59,5,11,'judge','两条直线相交成直角，这两条直线互相垂直。','','对','这是两条直线互相垂直的定义。'),
(60,5,12,'judge','平行四边形只有 4 条高。','','错','平行四边形可以画出无数条高。');

-- ============ 四年级英语 ============
INSERT INTO exam_paper(id,code,grade,subject,title,term,duration_min,note) VALUES
(6,'g4-english',4,'english','四年级上册英语期末模拟卷','上册',30,'教室与家庭 · 名词复数 · 日常问答');

INSERT INTO exam_question(id,paper_id,ordinal,kind,stem,options,answer,explain) VALUES
(61,6,1,'choice','—Where is my schoolbag? —It is（　）the desk.（在……上面）','on|in|under|near','A','on 表示在……上面。'),
(62,6,2,'choice','How many（　）are there in your class?','student|students|a student|studentes','B','How many 后面接可数名词复数。'),
(63,6,3,'choice','—What is in your classroom? —（　）','Many desks and chairs.|I am fine.|It is red.|Yes, I do.','A','问教室里有什么，回答桌椅等物品。'),
(64,6,4,'choice','My father''s brother is my（　）。','uncle|aunt|cousin|brother','A','爸爸的兄弟是叔叔或伯伯，英语用 uncle。'),
(65,6,5,'choice','—What would you like? —（　）','I would like some rice.|I like rice.|It is rice.|Yes, please.','A','What would you like 用 I would like… 回答。'),
(66,6,6,'fill','补全单词：t（　）cher（老师）','','ea','teacher 是老师。'),
(67,6,7,'fill','「厨房」的英文是（　）。','','kitchen','kitchen。'),
(68,6,8,'fill','—What is the weather like today? —It is（　）.（晴朗的）','','sunny','sunny 表示晴朗的。'),
(69,6,9,'fill','写出复数形式：box →（　）','','boxes','以 x 结尾的名词变复数加 es。'),
(70,6,10,'fill','「鸡肉」的英文是（　）。','','chicken','chicken。'),
(71,6,11,'judge','bedroom 的意思是「卧室」。','','对','bedroom 是卧室。'),
(72,6,12,'judge','I have two book. 这句话是正确的。','','错','two 后面要用复数 books。');

-- ============ 五年级语文 ============
INSERT INTO exam_paper(id,code,grade,subject,title,term,duration_min,note) VALUES
(7,'g5-chinese',5,'chinese','五年级上册语文期末模拟卷','上册',45,'说明方法 · 古诗积累 · 课文主旨');

INSERT INTO exam_question(id,paper_id,ordinal,kind,stem,options,answer,explain) VALUES
(73,7,1,'choice','「死去元知万事空，但悲不见九州同。」的作者是（　）。','陆游|辛弃疾|李清照|王安石','A','这两句出自陆游的《示儿》。'),
(74,7,2,'choice','《落花生》一文借花生告诉我们的道理是（　）。','要做有用的人|要长得好看|要多吃花生|要勤劳致富','A','父亲说花生虽然不好看，可是很有用，做人也要这样。'),
(75,7,3,'choice','「太阳会发光，会发热，是个大火球。」这句话使用的说明方法是（　）。','打比方|举例子|列数字|作比较','A','把太阳比作大火球，是打比方。'),
(76,7,4,'choice','「千丝万缕」中「缕」的意思是（　）。','线|量词|一丝|缠绕','A','「缕」本义是线，千丝万缕形容关系复杂。'),
(77,7,5,'choice','《题临安邸》中「暖风熏得游人醉，直把杭州作汴州」表达了诗人（　）的情感。','赞美|忧愤|喜悦|思念','B','诗人讽刺南宋统治者苟且偷安，满怀忧愤。'),
(78,7,6,'fill','《示儿》中「王师北定中原日，（　　）。」','','家祭无忘告乃翁','陆游临终嘱托儿子，统一时别忘了告诉他。'),
(79,7,7,'fill','《少年中国说》中「少年自由则国自由，（　　）。」','','少年进步则国进步','梁启超《少年中国说》中的排比句。'),
(80,7,8,'fill','《白鹭》中「白鹭是一首（　）的诗。」','','精巧','郭沫若《白鹭》的开头一句。'),
(81,7,9,'fill','「完璧归赵」的故事出自《（　）》。','','史记|廉颇蔺相如列传','出自《史记·廉颇蔺相如列传》。'),
(82,7,10,'fill','把成语补充完整：负（　）请罪。','','荆','负荆请罪，指主动认错赔礼。'),
(83,7,11,'judge','《太阳》一文运用了列数字、作比较、打比方等说明方法。','','对','课文用多种说明方法介绍太阳的特点。'),
(84,7,12,'judge','《珍珠鸟》的作者是老舍。','','错','《珍珠鸟》的作者是冯骥才。');

-- ============ 五年级数学 ============
INSERT INTO exam_paper(id,code,grade,subject,title,term,duration_min,note) VALUES
(8,'g5-math',5,'math','五年级上册数学期末模拟卷','上册',45,'小数乘除 · 简易方程 · 多边形面积');

INSERT INTO exam_question(id,paper_id,ordinal,kind,stem,options,answer,explain) VALUES
(85,8,1,'choice','0.25 × 4 = （　）','1|10|0.1|0.01','A','0.25 × 4 = 1。'),
(86,8,2,'choice','一个三角形的底是 6 厘米，高是 4 厘米，面积是（　）平方厘米。','24|12|10|20','B','三角形面积 = 底 × 高 ÷ 2 = 6 × 4 ÷ 2 = 12。'),
(87,8,3,'choice','下面各式中，是方程的是（　）。','3 + 5 = 8|2x - 4|x + 6 = 10|x > 3','C','含有未知数的等式才叫方程。'),
(88,8,4,'choice','3.6 ÷ 0.9 = （　）','4|0.4|40|0.04','A','36 ÷ 9 = 4，小数点位置不变。'),
(89,8,5,'fill','2.5 × 0.4 = （　）。','','1','25 × 4 = 100，两位小数，结果是 1。'),
(90,8,6,'fill','解方程 x + 7.5 = 12，x = （　）。','','4.5','x = 12 - 7.5 = 4.5。'),
(91,8,7,'fill','一个平行四边形的底是 8 厘米，高是 5 厘米，面积是（　）平方厘米。','','40','平行四边形面积 = 底 × 高 = 8 × 5 = 40。'),
(92,8,8,'fill','把 3.14 扩大到原来的 10 倍是（　）。','','31.4','小数点向右移动一位。'),
(93,8,9,'fill','一个梯形上底 4 厘米、下底 6 厘米、高 3 厘米，面积是（　）平方厘米。','','15','梯形面积 = （4 + 6）× 3 ÷ 2 = 15。'),
(94,8,10,'fill','5.6 ÷ 7 = （　）。','','0.8','56 ÷ 7 = 8，一位小数，结果是 0.8。'),
(95,8,11,'judge','两个面积相等的三角形一定能拼成一个平行四边形。','','错','要形状完全相同（全等）的两个三角形才能拼成。'),
(96,8,12,'judge','方程一定是等式。','','对','方程是含有未知数的等式，所以一定是等式。');

-- ============ 五年级英语 ============
INSERT INTO exam_paper(id,code,grade,subject,title,term,duration_min,note) VALUES
(9,'g5-english',5,'english','五年级上册英语期末模拟卷','上册',30,'人物描述 · 星期与课程 · can 与 there be');

INSERT INTO exam_question(id,paper_id,ordinal,kind,stem,options,answer,explain) VALUES
(97,9,1,'choice','—What is your maths teacher like? —（　）','He is tall and kind.|He likes music.|He is a teacher.|Yes, he is.','A','What is … like 是问人的外貌或性格。'),
(98,9,2,'choice','I often（　）books on Sundays.','read|reads|reading|to read','A','主语是 I，动词用原形。'),
(99,9,3,'choice','There（　）a big bed in my room.','is|are|be|am','A','a big bed 是单数，用 is。'),
(100,9,4,'choice','—Can you swim? —Yes, I（　）','can|do|am|could','A','Can 引导的问句用 can 回答。'),
(101,9,5,'choice','Are there（　）tall buildings in the park?','some|any|a|much','B','疑问句中一般用 any。'),
(102,9,6,'fill','补全单词：Satur（　）（星期六）','','day','Saturday 是星期六。'),
(103,9,7,'fill','「森林」的英文是（　）。','','forest','forest。'),
(104,9,8,'fill','写出「做作业」的英文短语：','','do homework|do my homework','do homework 是做作业。'),
(105,9,9,'fill','写出 go 的第三人称单数形式：（　）。','','goes','以 o 结尾的动词加 es。'),
(106,9,10,'fill','above 的意思是（　）。（填「在……上方」或「在……下方」）','','在……上方|在...上方|在上方|上方','above 表示在……上方。'),
(107,9,11,'judge','village 的意思是「村庄」。','','对','village 是村庄。'),
(108,9,12,'judge','She can sings well. 这句话是正确的。','','错','can 后面要用动词原形 sing。');

-- ============ 六年级语文 ============
INSERT INTO exam_paper(id,code,grade,subject,title,term,duration_min,note) VALUES
(10,'g6-chinese',6,'chinese','六年级上册语文期末模拟卷','上册',45,'文言文 · 古诗积累 · 词语搭配');

INSERT INTO exam_question(id,paper_id,ordinal,kind,stem,options,answer,explain) VALUES
(109,10,1,'choice','「野旷天低树，江清月近人。」出自（　）。','孟浩然《宿建德江》|王维《山居秋暝》|杜牧《秋夕》|李白《静夜思》','A','出自孟浩然的《宿建德江》。'),
(110,10,2,'choice','《伯牙鼓琴》中「伯牙鼓琴，锺子期听之」的「鼓」意思是（　）。','鼓声|弹|鼓掌|鼓励','B','「鼓」在这里是弹的意思。'),
(111,10,3,'choice','「黑云翻墨未遮山，白雨跳珠乱入船。」使用的修辞手法是（　）。','比喻|拟人|排比|反问','A','把黑云比作翻倒的墨汁，把雨点比作跳动的珍珠。'),
(112,10,4,'choice','《草原》的作者是（　）。','老舍|巴金|冰心|鲁迅','A','《草原》的作者是老舍。'),
(113,10,5,'choice','下列词语搭配不当的一项是（　）。','险峻的高山|清脆的歌声|幽静的闹市|辽阔的草原','C','「闹市」热闹喧哗，不能搭配「幽静」。'),
(114,10,6,'fill','《六月二十七日望湖楼醉书》中「卷地风来忽吹散，（　　）。」','','望湖楼下水如天','苏轼写雨后湖面平静如天。'),
(115,10,7,'fill','「（　　），万紫千红总是春。」','','等闲识得东风面','出自朱熹《春日》。'),
(116,10,8,'fill','《伯牙鼓琴》中「巍巍乎若太山」的「若」意思是（　）。','','像|好像','「若」是像、好像的意思。'),
(117,10,9,'fill','《狼牙山五壮士》中五位壮士跳崖的地点是（　）峰。','','棋盘陀','五位壮士在棋盘陀跳崖。'),
(118,10,10,'fill','「粉骨碎身浑不怕，要留清白在人间。」出自《（　）》。','','石灰吟','出自于谦的《石灰吟》。'),
(119,10,11,'judge','《穷人》的作者是俄国作家列夫·托尔斯泰。','','对','《穷人》是托尔斯泰的短篇小说。'),
(120,10,12,'judge','《桥》中的老汉先让儿子过桥，自己最后走。','','错','老汉先让群众过桥，最后才让儿子，父子都被洪水吞没。');

-- ============ 六年级数学 ============
INSERT INTO exam_paper(id,code,grade,subject,title,term,duration_min,note) VALUES
(11,'g6-math',6,'math','六年级上册数学期末模拟卷','上册',45,'分数乘除 · 比 · 圆 · 百分数');

INSERT INTO exam_question(id,paper_id,ordinal,kind,stem,options,answer,explain) VALUES
(121,11,1,'choice','3/4 × 8 = （　）','6|8/3|3/32|2','A','3/4 × 8 = 24/4 = 6。'),
(122,11,2,'choice','圆的半径是 2 厘米，周长是（　）厘米。（π 取 3.14）','6.28|12.56|3.14|25.12','B','周长 = 2πr = 2 × 3.14 × 2 = 12.56。'),
(123,11,3,'choice','一件商品原价 200 元，打八折后是（　）元。','160|180|40|120','A','200 × 80% = 160 元。'),
(124,11,4,'choice','甲数是乙数的 1/4，甲数与乙数的比是（　）。','1:4|4:1|1:5|5:1','A','把乙数看作 4 份，甲数就是 1 份。'),
(125,11,5,'fill','1/2 ÷ 1/4 = （　）。','','2','除以一个数等于乘它的倒数：1/2 × 4 = 2。'),
(126,11,6,'fill','一个圆的直径是 10 厘米，面积是（　）平方厘米。（π 取 3.14）','','78.5','半径 5 厘米，面积 = 3.14 × 25 = 78.5。'),
(127,11,7,'fill','25% 化成小数是（　）。','','0.25','百分数化小数，小数点向左移两位。'),
(128,11,8,'fill','把 3:4 的前项和后项都乘 2，比值是（　）。','','0.75|3/4','前后项同乘一个数，比值不变。'),
(129,11,9,'fill','一根绳子长 6 米，用去 1/3，还剩（　）米。','','4','6 × （1 - 1/3）= 4 米。'),
(130,11,10,'fill','六（1）班有 40 人，其中男生 24 人，男生占全班的（　）%。','','60','24 ÷ 40 = 60%。'),
(131,11,11,'judge','圆的周长总是直径的 π 倍。','','对','周长 = π × 直径。'),
(132,11,12,'judge','比的后项可以是 0。','','错','比的后项相当于除数，不能为 0。');

-- ============ 六年级英语 ============
INSERT INTO exam_paper(id,code,grade,subject,title,term,duration_min,note) VALUES
(12,'g6-english',6,'english','六年级上册英语期末模拟卷','上册',30,'问路与交通 · 一般将来时 · 职业');

INSERT INTO exam_question(id,paper_id,ordinal,kind,stem,options,answer,explain) VALUES
(133,12,1,'choice','—How do you go to school? —I go to school（　）foot.','by|on|in|at','B','步行是 on foot，固定搭配。'),
(134,12,2,'choice','—What are you going to do this weekend? —I am going to（　）。','visit my grandparents|visited my grandparents|visiting my grandparents|visits my grandparents','A','be going to 后面接动词原形。'),
(135,12,3,'choice','—What does your mother do? —She is a（　）。','teacher|teach|teaching|teaches','A','问职业，回答用名词。'),
(136,12,4,'choice','Turn left（　）the crossing.','at|in|on|to','A','在十字路口用 at the crossing。'),
(137,12,5,'choice','—How do you feel? —I feel（　）because I failed the test.','sad|happy|excited|hungry','A','考试没考好，心情是难过的。'),
(138,12,6,'fill','补全单词：hobb（　）（爱好）','','y','hobby 是爱好。'),
(139,12,7,'fill','「邮局」的英文是（　）。','','post office','post office。'),
(140,12,8,'fill','用所给词的适当形式填空：I will（　）(visit) my aunt next week.','','visit','will 后面接动词原形。'),
(141,12,9,'fill','写出「骑自行车」的英文短语：','','ride a bike|ride bikes','ride a bike 是骑自行车。'),
(142,12,10,'fill','subway 的意思是（　）。','','地铁','subway 是地铁。'),
(143,12,11,'judge','Where does he work? 是在问工作地点。','','对','where 问地点，work 是工作。'),
(144,12,12,'judge','He go to work by car. 这句话是正确的。','','错','主语是第三人称单数，动词要用 goes。');

-- ============ 七年级（初一）语文 ============
INSERT INTO exam_paper(id,code,grade,subject,title,term,duration_min,note) VALUES
(13,'g7-chinese',7,'chinese','七年级上册语文期末模拟卷','上册',60,'字音 · 古诗文默写 · 病句 · 名著');

INSERT INTO exam_question(id,paper_id,ordinal,kind,stem,options,answer,explain) VALUES
(145,13,1,'choice','下列加点字注音正确的一项是（　）。','酝酿（niàng）|静谧（bì）|憔悴（jiāo）|分歧（zhī）','A','静谧读 mì，憔悴读 qiáo，分歧读 qí。'),
(146,13,2,'choice','「日月之行，若出其中；星汉灿烂，若出其里。」出自（　）。','曹操《观沧海》|王湾《次北固山下》|李白《闻王昌龄左迁龙标遥有此寄》|马致远《天净沙·秋思》','A','出自曹操的《观沧海》。'),
(147,13,3,'choice','「学而不思则罔，思而不学则殆。」中「罔」的意思是（　）。','迷惑|欺骗|没有|网络','A','「罔」指迷惑而无所得。'),
(148,13,4,'choice','《春》的作者是（　）。','朱自清|老舍|冰心|鲁迅','A','《春》是朱自清的散文。'),
(149,13,5,'choice','下列句子没有语病的一项是（　）。','通过这次活动，使我明白了团结的重要。|我们要防止交通事故不再发生。|他的写作水平有了明显提高。|能否学好语文，关键在于要多读多写。','C','前三句分别有缺主语、否定不当、两面对一面的问题。'),
(150,13,6,'fill','《次北固山下》中「海日生残夜，（　　）。」','','江春入旧年','王湾《次北固山下》的名句。'),
(151,13,7,'fill','「我寄愁心与明月，（　　）。」','','随君直到夜郎西','李白《闻王昌龄左迁龙标遥有此寄》。'),
(152,13,8,'fill','《论语》中「三人行，必有我师焉。择其善者而从之，（　　）。」','','其不善者而改之','出自《论语·述而》。'),
(153,13,9,'fill','《天净沙·秋思》中「夕阳西下，（　　）。」','','断肠人在天涯','马致远《天净沙·秋思》的末句。'),
(154,13,10,'fill','《西游记》中唐僧的徒弟里，最憨厚贪吃的是（　　）。','','猪八戒','猪八戒憨厚、贪吃、爱偷懒。'),
(155,13,11,'judge','《济南的冬天》的作者是老舍，原名舒庆春。','','对','老舍原名舒庆春，字舍予。'),
(156,13,12,'judge','「闻王昌龄左迁龙标遥有此寄」中的「左迁」指升官。','','错','「左迁」是降职、贬官。');

-- ============ 七年级（初一）数学 ============
INSERT INTO exam_paper(id,code,grade,subject,title,term,duration_min,note) VALUES
(14,'g7-math',7,'math','七年级上册数学期末模拟卷','上册',60,'有理数 · 整式加减 · 一元一次方程 · 图形初步');

INSERT INTO exam_question(id,paper_id,ordinal,kind,stem,options,answer,explain) VALUES
(157,14,1,'choice','-5 的相反数是（　）。','5|-5|1/5|-1/5','A','只有符号不同的两个数互为相反数。'),
(158,14,2,'choice','下列各数中，最小的是（　）。','-3|0|2|-1','A','负数中绝对值大的反而小。'),
(159,14,3,'choice','单项式 -3x²y 的系数是（　）。','3|-3|-3x²|2','B','单项式中的数字因数是系数，含负号。'),
(160,14,4,'choice','方程 2x + 6 = 0 的解是（　）。','x = 3|x = -3|x = 6|x = -6','B','2x = -6，所以 x = -3。'),
(161,14,5,'fill','计算：(-2)³ = （　）。','','-8','3 个 -2 相乘，负数的奇次幂是负数。'),
(162,14,6,'fill','化简：3a - 5a = （　）。','','-2a','系数相减，字母部分不变。'),
(163,14,7,'fill','若 x = 2 是方程 3x + m = 10 的解，则 m = （　）。','','4','代入得 6 + m = 10，m = 4。'),
(164,14,8,'fill','一个角的补角是 130°，这个角是（　）度。','','50','互补两角之和为 180°。'),
(165,14,9,'fill','计算：|-7| - 3 = （　）。','','4','|-7| = 7，7 - 3 = 4。'),
(166,14,10,'fill','合并同类项：5x² - 2x² + x² = （　）。','','4x²|4x^2|4x2','系数 5 - 2 + 1 = 4。'),
(167,14,11,'judge','有理数包括正数和负数。','','错','有理数包括正数、负数和 0。'),
(168,14,12,'judge','两点之间，线段最短。','','对','这是线段的基本性质。');

-- ============ 七年级（初一）英语 ============
INSERT INTO exam_paper(id,code,grade,subject,title,term,duration_min,note) VALUES
(15,'g7-english',7,'english','七年级上册英语期末模拟卷','上册',45,'be 动词 · 一般现在时 · 代词 · 名词复数');

INSERT INTO exam_question(id,paper_id,ordinal,kind,stem,options,answer,explain) VALUES
(169,15,1,'choice','This is（　）English book.','a|an|the|不填','B','English 以元音音素开头，用 an。'),
(170,15,2,'choice','—（　）is your father? —He is fine, thank you.','How|What|Who|Where','A','How 问身体状况，答语是 fine。'),
(171,15,3,'choice','My sister（　）TV every evening.','watch|watches|watching|to watch','B','主语是第三人称单数，动词加 es。'),
(172,15,4,'choice','There（　）some milk in the glass.','is|are|have|has','A','milk 是不可数名词，用 is。'),
(173,15,5,'choice','—Whose bag is this? —It is（　）。','my|mine|me|I','B','名词性物主代词 mine 单独使用。'),
(174,15,6,'fill','写出复数形式：child →（　）。','','children','child 的复数是 children。'),
(175,15,7,'fill','用所给词的适当形式填空：Tom（　）(have) a new bike.','','has','第三人称单数用 has。'),
(176,15,8,'fill','「星期三」的英文是（　）。','','Wednesday','Wednesday 是星期三。'),
(177,15,9,'fill','补全答语：—Thank you very much. —You are（　）。','','welcome','You are welcome 是不用谢。'),
(178,15,10,'fill','写出序数词：one →（　）。','','first','one 的序数词是 first。'),
(179,15,11,'judge','He do not like apples. 这句话是正确的。','','错','第三人称单数要用 does not。'),
(180,15,12,'judge','不定冠词 an 用在以元音音素开头的单词前。','','对','an 用于元音音素开头的单词前。');

-- ============ 八年级（初二）语文 ============
INSERT INTO exam_paper(id,code,grade,subject,title,term,duration_min,note) VALUES
(16,'g8-chinese',8,'chinese','八年级上册语文期末模拟卷','上册',60,'文言文 · 古诗默写 · 成语 · 名著');

INSERT INTO exam_question(id,paper_id,ordinal,kind,stem,options,answer,explain) VALUES
(181,16,1,'choice','《三峡》中「两岸连山，略无阙处」的「阙」意思是（　）。','空缺|宫殿|通「缺」，空隙、缺口|停止','C','「阙」通「缺」，指缺口。'),
(182,16,2,'choice','「庭下如积水空明，水中藻、荇交横，盖竹柏影也。」出自（　）。','苏轼《记承天寺夜游》|郦道元《三峡》|吴均《与朱元思书》|陶弘景《答谢中书书》','A','出自苏轼的《记承天寺夜游》。'),
(183,16,3,'choice','「大漠孤烟直，长河落日圆。」的作者是（　）。','王维|李白|杜甫|王昌龄','A','出自王维的《使至塞上》。'),
(184,16,4,'choice','下列句子中加点成语使用不恰当的一项是（　）。','他做事一丝不苟，从不马虎。|这场演出精彩绝伦，观众掌声不断。|他说话咄咄逼人，让人听了很舒服。|春天来了，公园里百花齐放。','C','「咄咄逼人」是贬义，不会让人舒服。'),
(185,16,5,'choice','《红星照耀中国》的作者是（　）。','斯诺|史沫特莱|白求恩|斯特朗','A','美国记者埃德加·斯诺。'),
(186,16,6,'fill','《三峡》中「至于夏水襄陵，（　　）。」','','沿溯阻绝','郦道元《三峡》写夏水汹涌。'),
(187,16,7,'fill','「急湍甚箭，（　　）。」','','猛浪若奔','吴均《与朱元思书》。'),
(188,16,8,'fill','《野望》中「相顾无相识，（　　）。」','','长歌怀采薇','王绩《野望》的末句。'),
(189,16,9,'fill','《记承天寺夜游》中「但少闲人如吾两人者耳」表现了作者（　）的心境。','','闲适|旷达|豁达|乐观|闲适旷达','被贬黄州仍能赏月自适。'),
(190,16,10,'fill','《藤野先生》选自鲁迅的散文集《（　）》。','','朝花夕拾','《朝花夕拾》是鲁迅的散文集。'),
(191,16,11,'judge','《三峡》的作者郦道元是北魏地理学家。','','对','郦道元是北魏地理学家，著《水经注》。'),
(192,16,12,'judge','《答谢中书书》的作者是陶渊明。','','错','作者是南朝陶弘景。');

-- ============ 八年级（初二）数学 ============
INSERT INTO exam_paper(id,code,grade,subject,title,term,duration_min,note) VALUES
(17,'g8-math',8,'math','八年级上册数学期末模拟卷','上册',60,'三角形 · 全等 · 轴对称 · 整式乘除与因式分解 · 分式');

INSERT INTO exam_question(id,paper_id,ordinal,kind,stem,options,answer,explain) VALUES
(193,17,1,'choice','下列长度的三条线段能组成三角形的是（　）。','1,2,3|2,3,4|1,1,3|4,5,10','B','三角形任意两边之和要大于第三边。'),
(194,17,2,'choice','分解因式 x² - 9 = （　）。','(x-3)(x+3)|(x-9)(x+1)|(x-3)²|x(x-9)','A','平方差公式 a² - b² = (a+b)(a-b)。'),
(195,17,3,'choice','计算 (a³)² = （　）。','a⁵|a⁶|a⁹|a','B','幂的乘方，指数相乘：3 × 2 = 6。'),
(196,17,4,'choice','分式 1/(x-2) 有意义的条件是（　）。','x = 2|x ≠ 2|x > 2|x < 2','B','分母不能为 0，所以 x ≠ 2。'),
(197,17,5,'fill','计算：a² · a³ = （　）。','','a⁵|a^5|a5','同底数幂相乘，指数相加。'),
(198,17,6,'fill','若等腰三角形的顶角是 40°，则它的底角是（　）度。','','70','(180 - 40) ÷ 2 = 70。'),
(199,17,7,'fill','分解因式：2x² - 4x = （　）。','','2x(x-2)','提出公因式 2x。'),
(200,17,8,'fill','计算：(x+2)(x-3) = （　）。','','x²-x-6|x^2-x-6|x2-x-6','多项式乘法，合并同类项得 x² - x - 6。'),
(201,17,9,'fill','计算：(2²)³ = （　）。','','64','2² = 4，4³ = 64。'),
(202,17,10,'fill','全等三角形的对应边（　）。（填「相等」或「不一定相等」）','','相等','全等三角形对应边、对应角都相等。'),
(203,17,11,'judge','三角形的内角和是 180°。','','对','三角形内角和定理。'),
(204,17,12,'judge','分式的分子和分母都乘同一个数，分式的值不变。','','错','要乘同一个不等于 0 的整式，值才不变。');

-- ============ 八年级（初二）英语 ============
INSERT INTO exam_paper(id,code,grade,subject,title,term,duration_min,note) VALUES
(18,'g8-english',8,'english','八年级上册英语期末模拟卷','上册',45,'比较级 · 现在完成时 · 情态动词 · 动名词');

INSERT INTO exam_question(id,paper_id,ordinal,kind,stem,options,answer,explain) VALUES
(205,18,1,'choice','Tom is（　）than his brother.','tall|taller|tallest|the tallest','B','than 前面用比较级 taller。'),
(206,18,2,'choice','—Have you ever（　）to Beijing? —Yes, I have.','go|went|been|being','C','have been to 表示去过某地。'),
(207,18,3,'choice','You（　）be quiet in the library.','must|can|may|need','A','图书馆里必须安静，用 must。'),
(208,18,4,'choice','She（　）to the museum last Sunday.','go|goes|went|gone','C','last Sunday 是过去时间，用过去式。'),
(209,18,5,'choice','It is important（　）English every day.','practice|to practice|practicing|practiced','B','It is + 形容词 + to do 句型。'),
(210,18,6,'fill','写出比较级：good →（　）。','','better','good 的比较级是不规则变化 better。'),
(211,18,7,'fill','用所给词的适当形式填空：He（　）(finish) his homework already.','','has finished','already 提示用现在完成时。'),
(212,18,8,'fill','写出 invite 的名词形式：（　）。','','invitation','invite 的名词是 invitation。'),
(213,18,9,'fill','用所给词的适当形式填空：I am looking forward to（　）(see) you.','','seeing','look forward to 后面接动名词。'),
(214,18,10,'fill','写出 health 的形容词形式：（　）。','','healthy','health 的形容词是 healthy。'),
(215,18,11,'judge','the most beautiful 是 beautiful 的最高级。','','对','多音节形容词最高级加 the most。'),
(216,18,12,'judge','I have bought the book yesterday. 这句话是正确的。','','错','yesterday 是明确的过去时间，要用一般过去时 bought。');

-- ============ 九年级（初三）语文 ============
INSERT INTO exam_paper(id,code,grade,subject,title,term,duration_min,note) VALUES
(19,'g9-chinese',9,'chinese','九年级上册语文期末模拟卷','上册',60,'文言文 · 古诗默写 · 病句 · 名著');

INSERT INTO exam_question(id,paper_id,ordinal,kind,stem,options,answer,explain) VALUES
(217,19,1,'choice','「先天下之忧而忧，后天下之乐而乐。」出自（　）。','范仲淹《岳阳楼记》|欧阳修《醉翁亭记》|苏轼《水调歌头》|刘禹锡《酬乐天扬州初逢席上见赠》','A','出自范仲淹的《岳阳楼记》。'),
(218,19,2,'choice','「醉翁之意不在酒，在乎山水之间也。」中「意」的意思是（　）。','意思|情趣|意见|心意','B','「意」在这里指情趣。'),
(219,19,3,'choice','「但愿人长久，千里共婵娟。」中「婵娟」指的是（　）。','美女|月亮|嫦娥|花朵','B','「婵娟」在这里代指月亮。'),
(220,19,4,'choice','下列句子没有语病的一项是（　）。','我们要及时纠正并发现学习中的问题。|通过阅读名著，使我受益匪浅。|这本书的内容和插图都很精美。|学校开展了「书香校园」读书活动。','D','前三句分别有语序不当、缺主语、搭配不当的问题。'),
(221,19,5,'choice','《我的叔叔于勒》的作者是（　）。','莫泊桑|契诃夫|欧·亨利|雨果','A','法国作家莫泊桑。'),
(222,19,6,'fill','《岳阳楼记》中「不以物喜，（　　）。」','','不以己悲','范仲淹《岳阳楼记》。'),
(223,19,7,'fill','「沉舟侧畔千帆过，（　　）。」','','病树前头万木春','刘禹锡《酬乐天扬州初逢席上见赠》。'),
(224,19,8,'fill','「长风破浪会有时，（　　）。」','','直挂云帆济沧海','李白《行路难》。'),
(225,19,9,'fill','《醉翁亭记》中「醉能同其乐，醒能述以文者，太守也」，这里的太守是（　　）。','','欧阳修','欧阳修自号醉翁，当时任滁州太守。'),
(226,19,10,'fill','「为什么我的眼里常含泪水？因为我对这土地爱得深沉……」出自艾青的《（　）》。','','我爱这土地','艾青的《我爱这土地》。'),
(227,19,11,'judge','《水调歌头·明月几时有》的作者是苏轼。','','对','这首词是苏轼中秋所作。'),
(228,19,12,'judge','《故乡》选自鲁迅的小说集《朝花夕拾》。','','错','《故乡》选自小说集《呐喊》。');

-- ============ 九年级（初三）数学 ============
INSERT INTO exam_paper(id,code,grade,subject,title,term,duration_min,note) VALUES
(20,'g9-math',9,'math','九年级上册数学期末模拟卷','上册',60,'一元二次方程 · 二次函数 · 圆 · 概率 · 锐角三角函数');

INSERT INTO exam_question(id,paper_id,ordinal,kind,stem,options,answer,explain) VALUES
(229,20,1,'choice','一元二次方程 x² - 4 = 0 的解是（　）。','x = 2|x = ±2|x = 4|x = ±4','B','x² = 4，所以 x = 2 或 x = -2。'),
(230,20,2,'choice','抛物线 y = (x-1)² + 2 的顶点坐标是（　）。','(1,2)|(-1,2)|(1,-2)|(2,1)','A','顶点式 y = a(x-h)² + k 的顶点是 (h,k)。'),
(231,20,3,'choice','掷一枚质地均匀的硬币，正面朝上的概率是（　）。','1/2|0|1|1/4','A','两种结果等可能，概率是 1/2。'),
(232,20,4,'choice','在 Rt△ABC 中，∠C = 90°，sinA = 3/5，则 cosA = （　）。','3/5|4/5|5/4|3/4','B','sin²A + cos²A = 1，cosA = 4/5。'),
(233,20,5,'fill','方程 x² - 5x + 6 = 0 较小的根是（　）。','','2','因式分解得 (x-2)(x-3) = 0，两根为 2 和 3。'),
(234,20,6,'fill','二次函数 y = x² - 2x 的对称轴是直线 x =（　）。','','1','对称轴 x = -b/2a = 2/2 = 1。'),
(235,20,7,'fill','一个扇形的半径是 3，圆心角是 60°，它的面积是（　）π。','','1.5','S = 60/360 × π × 9 = 1.5π。'),
(236,20,8,'fill','若两个相似三角形的相似比是 1:2，则它们的面积比是 1:（　）。','','4','面积比等于相似比的平方。'),
(237,20,9,'fill','计算：2cos60° = （　）。','','1','cos60° = 1/2，所以结果是 1。'),
(238,20,10,'fill','关于 x 的方程 x² - 2x + k = 0 有两个相等的实数根，则 k =（　）。','','1','判别式 4 - 4k = 0，得 k = 1。'),
(239,20,11,'judge','直径所对的圆周角是直角。','','对','圆周角定理的推论。'),
(240,20,12,'judge','二次函数 y = -x² 的图象开口向上。','','错','a = -1 < 0，开口向下。');

-- ============ 九年级（初三）英语 ============
INSERT INTO exam_paper(id,code,grade,subject,title,term,duration_min,note) VALUES
(21,'g9-english',9,'english','九年级上册英语期末模拟卷','上册',45,'定语从句 · 被动语态 · 宾语从句 · 感叹句');

INSERT INTO exam_question(id,paper_id,ordinal,kind,stem,options,answer,explain) VALUES
(241,21,1,'choice','This is the boy（　）helped me yesterday.','which|who|whom|what','B','先行词是人且在从句中作主语，用 who。'),
(242,21,2,'choice','The window（　）by Tom yesterday.','broke|is broken|was broken|breaks','C','yesterday 提示一般过去时的被动语态。'),
(243,21,3,'choice','Could you tell me（　）?','where is the bank|where the bank is|where was the bank|the bank where is','B','宾语从句要用陈述语序。'),
(244,21,4,'choice','He used to（　）in the countryside.','live|living|lived|lives','A','used to 后面接动词原形。'),
(245,21,5,'choice','（　）interesting book it is!','What|What an|How|How an','B','感叹句 What + a/an + 形容词 + 名词。'),
(246,21,6,'fill','用所给词的适当形式填空：If it（　）(rain) tomorrow, we will stay at home.','','rains','if 从句用一般现在时表将来。'),
(247,21,7,'fill','用被动语态填空：English（　）(speak) in many countries.','','is spoken','English 作主语，用一般现在时被动语态。'),
(248,21,8,'fill','填入恰当的关系代词：The book（　）I bought yesterday is very interesting.','','which|that','先行词是物，关系代词用 which 或 that。'),
(249,21,9,'fill','写出 succeed 的名词形式：（　）。','','success','succeed 的名词是 success。'),
(250,21,10,'fill','用所给词的适当形式填空：He is good at（　）(play) basketball.','','playing','be good at 后面接动名词。'),
(251,21,11,'judge','I do not know if he will come. 句中 if 引导的是宾语从句。','','对','if 在动词后引导宾语从句，意为「是否」。'),
(252,21,12,'judge','He has gone to Beijing for three days. 这句话是正确的。','','错','gone 是短暂性动词，不能与表示一段时间的 for three days 连用。');
