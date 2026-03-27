import { SweetEditor } from 'sweeteditor'
import { window } from '@kit.ArkUI'

@Entry
@Component
struct Index {
  @State message: string = 'Hello World';
  @State topPadding: number = 0;

  aboutToAppear(): void {
    const uiContext = this.getUIContext();
    const hostContext = uiContext.getHostContext();
    if (!hostContext) {
      return;
    }
    window.getLastWindow(hostContext).then((win) => {
      const avoidArea = win.getWindowAvoidArea(window.AvoidAreaType.TYPE_SYSTEM);
      this.topPadding = uiContext.px2vp(avoidArea.topRect.height);
    }).catch(() => {
    });
  }

  build() {
    Column() {
      SweetEditor()
        .layoutWeight(1)
        .width('100%')
    }
    .height('100%')
    .width('100%')
    .padding({ top: this.topPadding })
  }
}
