# 일정 관리 앱(MyScheduler)

## Screenshots

### 메인 화면
<img src="screenshots/image1.png" width="800"/>

### 일정 추가 / 수정 화면
<img src="screenshots/image2.png" width="800"/>

### 일정 상세 조회
<img src="screenshots/image3.png" width="800"/>


## 프로젝트 소개

**MyScheduler**는 WPF(.NET 8)와 MVVM 패턴으로 만든 일정 관리 앱 개인 프로젝트 입니다.

CRUD 구현과 비동기 UI 환경에서 발생할 수 있는 **레이스 컨디션 방지**, 확장 시 **동시성 문제 개선**하여 데이터 무결성 유지하도록 설계했습니다.

---

## 기술 스택

- **Language**: C#
- **Framework**: .NET 8.0 (WPF)
- **Architecture**: MVVM
- **MVVM Toolkit**: CommunityToolkit.Mvvm
- **ORM**: Entity Framework Core
- **Database**: MSSQL(SQL Server)
- **IDE**: Visual Studio 2022

---

## 아키텍처 구조

```
User
 ↓
View (XAML: DataBinding / Command Trigger)
 ↓
ViewModel (상태/Command, 비동기 UI 갱신 제어(requestVersion), Busy/Loading)
 ↓
Service (업무 규칙, UTC↔KST 변환, 동시성 충돌 감지/예외)
 ↓
DbContextFactory (IDbContextFactory<AppDbContext>)
 ↓
EF Core (AppDbContext / Migrations)
 ↓
SQL Server (MySchedulerDb)


```

- ViewModel과 Service를 중심으로 UI 상태와 업무 규칙을 분리하고 DbContextFactory 기반 데이터 접근을 통해 WPF 환경에 적합한 실행 안정성을 확보한 구조
- DbContextFactory를 사용하여 필요할 때마다 DbContext 인스턴스를 생성/폐기하며 데이터 접근을 관리
	- 안전한 데이터 접근을 위해 IDbContextFactory<AppDbContext> 사용
	- 모든 작업은 CreateDbContext()로 새 DbContext를 생성하고 즉시 폐기
	- DbContext는 비동기 자원 해제가 가능하기 때문에 await using을 사용하면 DB 연결/리소스 해제를 안전하게 기다린 뒤 다음 코드로 넘어감
		- Dispose가 끝나기 전에 흐름이 종료되는 것을 방지하여 자원 효율 좋음

---

## 주요 기능

- 일정 목록 조회, 일정 상세 조회, 일정 추가 / 수정 / 삭제, MVVM 기반 DataBinding & Command

- 의존성 관리
  - ViewModel과 Service를 분리하여 UI 로직과 업무 규칙의 책임을 명확히 분리
  - 실행 시작 지점에서 의존성을 구성하여 객체 생성과 수명 관리 흐름을 일관되게 유지

- 상태 중심 UI 제어
  - ViewModel의 상태 값(IsBusy, IsLoadingList 등)을 기준으로 Command 실행 가능 여부를 제어
  - 로딩/처리 중에는 중복 요청을 방지하고, UI 상태가 항상 현재 작업 흐름과 일치하도록 관리
  - UI 이벤트 처리와 업무 규칙을 ViewModel과 Service로 분리하여 책임을 명확히 분리

- CSV 내보내기
  - 현재 화면에 표시된 일정 목록을 CSV 파일로 저장
  - 검색/필터가 적용된 상태를 그대로 반영하여 화면과 결과 파일의 데이터 일관성 유지
  - DB를 재조회하지 않고 ViewModel 목록을 사용하여 성능과 안정성 확보
  - UTF-8 인코딩을 사용해 엑셀에서 한글 깨짐 없이 바로 열기 가능

- 목록 페이징
  - 일정 목록을 페이지당 10개로 분할 표시
  - 이전/페이지 번호/다음 버튼으로 페이지 이동
  - 검색/필터 결과 기준으로 페이징이 동적으로 갱신되어 일관된 탐색 경험 제공

 
---

## 비동기 처리 & UX 안정성

### 레이스 컨디션 방지(비동기 UI 갱신 문제)

- 날짜 변경이나 일정 목록 선택을 빠르게 반복할 경우에는 이전 비동기 요청이 늦게 완료되면서 최신 사용자 선택을 덮어쓰는 문제 발생
  - 예) 6월 1일 요청 A → 6월 2일 요청 B → B가 먼저 완료 → A가 늦게 완료되어 화면 덮어씀

**해결**

- 목록 조회와 상세 조회 요청마다 요청 버전(requestVersion) 을 증가
- 응답 수신 시점에 현재 요청 버전과 비교하여 최신 요청만 UI에 반영
- 이전 요청의 응답은 무시하도록 처리

 → 빠른 날짜 전환이나 선택 변경에도 화면 상태가 항상 사용자 입력과 일치

### 비동기 요청 취소

- 날짜 변경/선택 변경이 빠르게 연속 발생할 때, 이전 요청이 계속 실행되어 불필요한 DB 작업과 지연 발생

**해결**

- 목록 조회/상세 조회마다 CancellationTokenSource 생성
- 새 요청이 들어오면 이전 요청은 Cancel 처리
- 취소된 요청 결과는 UI에 반영하지 않음

 → 불필요한 작업을 줄이고, 최신 사용자 입력에만 반응하도록 개선

### Write 작업 안정화(재진입 방지)

- 일정 추가/수정/삭제 작업이 연속 또는 동시에 실행될 경우는 중복 처리, 상태 꼬임, 의도하지 않은 요청 발생 가능
- 쓰기 작업과 목록 로딩이 동시에 진행되며 UI 상태 충돌 우려

**해결**

- ViewModel에서 IsBusy, IsLoadingList 상태를 분리 관리
- 상태 값 기준으로 Command 실행 가능 여부(CanExecute) 제어
- 처리 중에는 버튼 비활성화 및 로딩 오버레이 표시로 사용자 입력 제한

 → 연타/중복 실행을 방지하고, 쓰기/조회 작업 간 UI 상태 충돌 없는 안정적인 흐름 유지


### 동시성 개선

- 동일한 일정을 여러 창에서 동시에 수정할 수 있는 상황에서 최신 변경 사항을 인지하지 못한 채 저장 시도 시 데이터 유실 가능
  - 예) 창 A, B 모두 RowVersion=1 → A 저장(RowVersion=2) → B가 이전 RowVersion=1 으로 저장 시도 → 덮어쓰기

**해결**

- RowVersion 기반 낙관적 동시성 제어 적용
- 수정·삭제 시점에 EF Core가 데이터 변경 여부 검증
- 충돌 발생 시 작업 중단
  - 삭제된 경우 → 사용자 알림 후 목록 갱신
  - 수정 충돌인 경우 → 최신 데이터를 재조회하여 화면 갱신

 → 예상하지 못한 덮어쓰기와 데이터 유실 방지, 사용자가 항상 현재 기준의 정확한 데이터 상태를 인지 가능

### 클라우드 배포 대응 시간 처리

- 로컬과 클라우드(AWS/Azure) 환경 간 서버 시간 차이로 일정 시간 오차 발생 가능
- 이전 EC2 배포 경험에서 실제 시간 불일치 문제 확인

**해결**

- DB에는 UTC 기준으로 일정 저장하고, 화면 표시 및 사용자 입력은 KST 기준으로 처리
- Service 계층에서 UTC ↔ KST 변환을 일관되게 적용

 → 배포 환경과 무관하게 동일한 시간 기준 유지하고, 클라우드 환경에서도 일정 시간 오차 없는 안정적인 동작 보장

### 검색 및 필터

- 일정 수가 많을수록 탐색 비용 증가, 검색/필터마다 DB 재조회 시 불필요한 성능 낭비 발생

**해결**

- 일정 목록은 날짜 변경 시에만 DB 조회하고, 검색 및 필터는 ViewModel의 ObservableCollection과 CollectionView를 활용
- 화면 내 데이터에 대해 즉시 필터링 적용

 → 빠른 화면 반응성 유지하고, 불필요한 DB 접근 제거로 성능과 안정성 확보

### 검색 대소문자 완화

- 대소문자/공백 입력 차이로 검색 결과가 달라져 사용자 경험이 불안정할 수 있음(abc 검색하면 abc만 나옴)

**해결**

- 검색어를 공백 정리 후 정규화(대소문자 통일)하여 비교
- 입력 형태가 달라도 동일한 의미의 검색이 되도록 처리

 → 대소문자/공백 입력 차이에도 일관된 검색 결과 제공
