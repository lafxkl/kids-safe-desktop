import { PageId, router } from '../core/Router';
import { screenTime } from '../core/ScreenTime';
import { settings } from '../core/Settings';
import { BreathingPage } from './BreathingPage';
import { CareCenterPage } from './CareCenterPage';
import { CareLessonDetailPage } from './CareLessonDetailPage';
import { CareLessonPage } from './CareLessonPage';
import { ExamListPage } from './ExamListPage';
import { ExamPage } from './ExamPage';
import { HomePage } from './HomePage';
import { GamePage } from './GamePage';
import { LeaderboardPage } from './LeaderboardPage';
import { ReadingPage } from './ReadingPage';
import { LearnWorldPage } from './LearnWorldPage';
import { LetterToParentsPage } from './LetterToParentsPage';
import { LockoutPage } from './LockoutPage';
import { FeedbackPage } from './FeedbackPage';
import { MakerPage } from './MakerPage';
import { MoodJournalPage } from './MoodJournalPage';
import { OnboardingPage } from './OnboardingPage';
import { OralPracticePage } from './OralPracticePage';
import { ParentDashboardPage } from './ParentDashboardPage';
import { ParentGatePage } from './ParentGatePage';
import { PoemDetailPage } from './PoemDetailPage';
import { PoemWorldPage } from './PoemWorldPage';
import { RhymeDetailPage } from './RhymeDetailPage';
import { RhymeWorldPage } from './RhymeWorldPage';
import { SafetyDetailPage } from './SafetyDetailPage';
import { SafetyQuizPage } from './SafetyQuizPage';
import { SafetyWorldPage } from './SafetyWorldPage';
import { SplashPage } from './SplashPage';
import { StarShopPage } from './StarShopPage';
import { StoryHousePage } from './StoryHousePage';
import { StoryReaderPage } from './StoryReaderPage';

@Entry
@ComponentV2
struct Index {
  aboutToAppear(): void {
    // 时间到点由计时器主动通知，而不是靠界面轮询
    screenTime.onLocked(() => {
      if (router.current !== PageId.Gate && router.current !== PageId.Dashboard) {
        router.push(PageId.Lockout);
      }
    });
  }

  build() {
    Stack() {
      if (router.current === PageId.Splash) {
        SplashPage()
      } else if (router.current === PageId.Onboarding) {
        OnboardingPage()
      } else if (router.current === PageId.Home) {
        HomePage()
      } else if (router.current === PageId.Gate) {
        ParentGatePage()
      } else if (router.current === PageId.Lockout) {
        LockoutPage()
      } else if (router.current === PageId.Dashboard) {
        ParentDashboardPage()
      } else if (router.current === PageId.StoryHouse) {
        StoryHousePage()
      } else if (router.current === PageId.StoryReader) {
        StoryReaderPage()
      } else if (router.current === PageId.PoemWorld) {
        PoemWorldPage()
      } else if (router.current === PageId.PoemDetail) {
        PoemDetailPage()
      } else if (router.current === PageId.RhymeWorld) {
        RhymeWorldPage()
      } else if (router.current === PageId.RhymeDetail) {
        RhymeDetailPage()
      } else if (router.current === PageId.SafetyWorld) {
        SafetyWorldPage()
      } else if (router.current === PageId.SafetyDetail) {
        SafetyDetailPage()
      } else if (router.current === PageId.SafetyQuiz) {
        SafetyQuizPage()
      } else if (router.current === PageId.LearnWorld) {
        LearnWorldPage()
      } else if (router.current === PageId.OralPractice) {
        OralPracticePage()
      } else if (router.current === PageId.ExamList) {
        ExamListPage()
      } else if (router.current === PageId.Exam) {
        ExamPage()
      } else if (router.current === PageId.Leaderboard) {
        LeaderboardPage()
      } else if (router.current === PageId.StarShop) {
        StarShopPage()
      } else if (router.current === PageId.CareCenter) {
        CareCenterPage()
      } else if (router.current === PageId.MoodJournal) {
        MoodJournalPage()
      } else if (router.current === PageId.CareLesson) {
        CareLessonPage()
      } else if (router.current === PageId.CareLessonDetail) {
        CareLessonDetailPage()
      } else if (router.current === PageId.Breathing) {
        BreathingPage()
      } else if (router.current === PageId.Reading) {
        ReadingPage()
      } else if (router.current === PageId.LetterToParents) {
        LetterToParentsPage()
      } else if (router.current === PageId.Game) {
        GamePage({ gameKey: router.gameKey })
      } else if (router.current === PageId.Maker) {
        MakerPage()
      } else if (router.current === PageId.Feedback) {
        FeedbackPage()
      }

      // 护眼滤镜盖在最上层，且不拦触摸
      if (settings.eyeCareEnabled) {
        Column()
          .width('100%')
          .height('100%')
          .backgroundColor('#FFB020')
          .opacity(settings.eyeCareIntensity * 0.45)
          .hitTestBehavior(HitTestMode.None)
      }
    }
    .width('100%')
    .height('100%')
    .backgroundColor('#FFF7EF')
  }

  /**
   * 拦截返回键。
   * 儿童桌面和锁屏页返回 true（什么也不做）——按返回键就退出应用，
   * 那是 Windows 端最容易被孩子发现的漏洞，鸿蒙这边堵死。
   */
  onBackPress(): boolean {
    if (router.current === PageId.Splash || router.current === PageId.Home || router.current === PageId.Lockout) {
      return true;
    }
    router.back();
    return true;
  }
}
