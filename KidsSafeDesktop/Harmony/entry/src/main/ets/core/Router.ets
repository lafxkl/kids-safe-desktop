/**
 * 极简页面栈，对应 Linux 端 QML 里的 StackView。
 *
 * 不用 Navigation + NavDestination 的原因：这套界面是一台「儿童终端」，
 * 没有前进后退语义，也不需要标题栏动画。用显式栈反而能保证一件事——
 * 孩子在任何页面狂点，栈深度都是可控的，不会出现层层叠加退不出去。
 */
export enum PageId {
  Splash = 'splash',
  Onboarding = 'onboarding',
  Home = 'home',
  Gate = 'gate',
  Lockout = 'lockout',
  Dashboard = 'dashboard',
  StoryHouse = 'storyHouse',
  StoryReader = 'storyReader',
  PoemWorld = 'poemWorld',
  PoemDetail = 'poemDetail',
  RhymeWorld = 'rhymeWorld',
  RhymeDetail = 'rhymeDetail',
  SafetyWorld = 'safetyWorld',
  SafetyDetail = 'safetyDetail',
  /** 安全小问答：从 40 道题里抽 8 道，答完给讲解。 */
  SafetyQuiz = 'safetyQuiz',
  /** 学习天地：口算练习 + 模拟考试两个模式共用一张年级表。 */
  LearnWorld = 'learnWorld',
  /** 口算练习（一题一屏，立刻判对错）。 */
  OralPractice = 'oralPractice',
  /** 某年级的模拟卷列表。 */
  ExamList = 'examList',
  /** 模拟考试答题页。 */
  Exam = 'exam',
  /** 总排行榜：所有小游戏和知识模块按星星排名。 */
  Leaderboard = 'leaderboard',
  /** 星星中心 + 兑换商店。 */
  StarShop = 'starShop',
  /** 关怀中心：心情打卡、暖心话、求助热线。 */
  CareCenter = 'careCenter',
  /** 心情日记：14 天趋势与全部记录。 */
  MoodJournal = 'moodJournal',
  /** 心理小课堂列表（孩子 / 家长两套）。 */
  CareLesson = 'careLesson',
  /** 某一节课的正文。 */
  CareLessonDetail = 'careLessonDetail',
  /** 4-7-8 呼吸放松。 */
  Breathing = 'breathing',
  /** 绘本怎么找：免费合法来源 + 经典绘本去哪借（清单见 data/ReadingData.ets）。 */
  Reading = 'reading',
  /** 想对爸妈说（只存本机）。 */
  LetterToParents = 'letterToParents',
  /** 内置游戏（猫国 / 象棋 / 植物大战僵尸 / 蛇蛇方块），走 Web 组件加载离线包。 */
  Game = 'game',
  /** 小游戏工坊：家长用网页编辑器做游戏（四端共用同一份 H5）。 */
  Maker = 'maker',
  /** 意见反馈 / 满意度 / 问卷（数据进官网）。 */
  Feedback = 'feedback'
}

/** 家长验证弹层验证通过后要去哪。 */
export enum GateIntent {
  Dashboard = 'dashboard',
  Extend = 'extend'
}

@ObservedV2
export class Router {
  @Trace current: PageId = PageId.Splash;
  @Trace gateIntent: GateIntent = GateIntent.Dashboard;
  /** 阅读页要打开的故事 id，进页面前由列表页写入。 */
  @Trace storyId: number = 0;

  /**
   * 详情页要显示哪一条。列表页把「筛选条件 + 下标」写进来，详情页据此翻上一首/下一首。
   * 只传下标不传数据：详情页自己按分类重算列表，两边引用同一份常量数据，不会不同步。
   */
  @Trace poemCategory: string = '全部';
  @Trace poemIndex: number = 0;
  @Trace rhymeIndex: number = 0;
  @Trace safetyIndex: number = 0;

  /** 学习天地选的是几年级（1~9）。口算练习和模拟考都读它。 */
  @Trace learnGrade: number = 3;
  /** 要打开哪一套模拟卷（试卷 id）。 */
  @Trace examPaperId: number = 1;

  /** 心理小课堂看的是哪一套：kids / parents。列表页和详情页共用。 */
  @Trace careAudience: string = 'kids';
  /** 要打开哪一节课（课程 id）。 */
  @Trace careLessonId: string = '';

  /**
   * 要打开哪个内置游戏：kittens / xiangqi / pvz / snake。
   * 首页磁贴先写这个字段，再 push(PageId.Game)，游戏页按它选 rawfile。
   */
  @Trace gameKey: string = 'xiangqi';

  private stack: PageId[] = [PageId.Splash];

  push(page: PageId): void {
    this.stack.push(page);
    this.current = page;
  }

  back(): boolean {
    if (this.stack.length <= 1) {
      return false;
    }
    this.stack.pop();
    this.current = this.stack[this.stack.length - 1];
    return true;
  }

  replace(page: PageId): void {
    if (this.stack.length === 0) {
      this.stack.push(page);
    } else {
      this.stack[this.stack.length - 1] = page;
    }
    this.current = page;
  }

  /**
   * 一路退回到指定页面（它必须在栈里，否则当成 push）。
   * 用途：详情页里「去看求助电话」这类跳转要回到上两层的首页，
   * 连续调 back() 会让「退不出去」和「退过头」都变得可能。
   */
  popTo(page: PageId): void {
    const at: number = this.stack.lastIndexOf(page);
    if (at < 0) {
      this.push(page);
      return;
    }
    this.stack = this.stack.slice(0, at + 1);
    this.current = page;
  }

  reset(page: PageId): void {
    this.stack = [page];
    this.current = page;
  }
}

export const router: Router = new Router();
