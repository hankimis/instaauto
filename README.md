## InstagramDMSender.Avalonia

Avalonia(.NET 9) 기반 Instagram DM 자동화 GUI 애플리케이션입니다. Selenium WebDriver(Chrome)와 Selenium Manager를 사용해 드라이버를 자동으로 해결합니다.

### 요구 사항
- **운영체제**: macOS (테스트: macOS 14.x)
- **런타임/SDK**: .NET SDK 9.0.x
- **브라우저**: Google Chrome 최신 안정 버전

### 빠른 시작 (macOS)
1) 저장소(프로젝트) 디렉토리로 이동
```bash
cd "/Users/hankim/Desktop/자동화/InstagramDMSender.Avalonia"
```

2) 복원/빌드 (Release)
```bash
dotnet restore
dotnet build -c Release
```

3) 실행 (소스에서 바로 실행)
```bash
dotnet run -c Release --project InstagramDMSender.Avalonia.csproj
```

또는 배포용 바이너리로 게시 후 실행할 수 있습니다.

4) 게시(publish) 후 실행
```bash
# Apple Silicon (arm64)
dotnet publish -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true

# 실행 파일 위치 (예)
./bin/Release/net9.0/osx-arm64/publish/InstagramDMSender.Avalonia
```

참고: 게시 결과에 .app 번들이 생성되지 않는 경우가 있습니다. 위 경로의 바이너리(실행 파일)를 직접 실행하면 됩니다.

### Windows/Linux (선택 사항)
- Windows x64 게시
```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

- Linux x64 게시
```bash
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

### 실행 전 유의사항
- Chrome가 설치되어 있어야 합니다. Selenium Manager가 실행 시점에 Chrome 버전에 맞는 드라이버를 자동으로 다운로드/설정합니다.
- 네트워크 환경에서 드라이버 다운로드가 차단되면 최초 실행이 실패할 수 있습니다. 방화벽/프록시 정책을 확인하세요.
- 2단계 인증/캡차가 나타나면 수동으로 처리해야 합니다.

### 사용 방법 (UI)
1) 앱 실행 후, 우측의 `아이디`, `비밀번호`에 Instagram 계정 정보를 입력합니다. (선택 사항)
2) 상단 `🌐 브라우저 시작` 버튼을 클릭합니다.
   - 계정 정보가 입력되어 있으면 자동 로그인 시도
   - 비워둔 경우 Instagram 로그인/인박스 페이지로 열림
3) 중앙 `보내실 DM 작성` 영역에 메시지 템플릿을 입력합니다.
   - 수신자 표시 이름을 삽입하려면 `<Username>` 플레이스홀더를 사용하세요.
   - 스핀텍스트 지원: 예) `{안녕하세요|반갑습니다}` → 무작위 선택
4) 우측 `아이디 등록 및 작업설정`에서 DM 대상 사용자명을 쉼표로 구분하여 입력 후 `추가` 클릭
5) 필요 시 `필터 설정`(게시물/팔로워 범위, 공개/비공개)을 조정합니다.
6) `▶️ 전송 시작`을 눌러 전송을 시작합니다. 진행 상황/로그는 하단에 표시됩니다.
7) 중지하려면 `⏹️ 중지`를 클릭합니다.

### 메시지 템플릿 규칙
- 표시 이름 치환: `<Username>`
- 스핀텍스트: `{옵션1|옵션2|옵션3}` 형태 지원

### 문제 해결
- Chrome이 뜨지 않거나 드라이버 오류 발생: 최신 Chrome 설치 상태 및 네트워크 정책(드라이버 다운로드 허용) 확인
- 로그인 실패 반복: 계정 자격 증명, 2FA/보안 경고, IP 차단 여부 확인 후 재시도
- 전송 버튼 탐지 실패: Instagram UI 변경 가능성이 있어 일시적으로 실패할 수 있습니다. 재시도하거나 간격을 늘려 시도하세요.

### 라이선스/책임
이 도구 사용으로 인한 계정 제한/차단 등 모든 책임은 사용자에게 있습니다. Instagram의 이용약관과 관련 법규를 준수하세요.


