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
View (화면 렌더링, DataBinding & Command)
 ↓
ViewModel (상태 관리, Command 처리, 비동기 요청 흐름 제어)
 ↓
Service (비즈니스 로직)
 ↓
Repository (ORM / Data Access)
 ↓
Database (MSSQL)

```

---

## 주요 기능

- 일정 목록 조회, 일정 상세 조회, 일정 추가 / 수정 / 삭제, MVVM 기반 DataBinding & Command

- DI
  - ViewModel과 Service 간 결합도 낮추기 위해 의존성 주입
  - 객체 생성을 App으로 분리하여 일관성 확보

- 상태 중심 UI 제어
  - ViewModel 상태 값(IsBusy, Loading 등)을 기준으로 동작 제어
  - UI 로직과 비즈니스 로직 분리
---

## 비동기 처리 & UX 안정성

### 레이스 컨디션 방지(비동기 UI 갱신 문제)

- 날짜 변경이나 일정 목록 선택을 빠르게 반복할 경우에는 이전 비동기 요청이 늦게 완료되면서 최신 사용자 선택을 덮어씀
- 화면에 잘못된 데이터가 표시될 수 있는 문제 존재

**해결**

- 비동기 요청마다 증가하는 버전 토큰(requestVersion)을 부여
- 가장 마지막에 요청한 작업의 응답만 UI에 반영하도록 처리
    
 -> 비동기 환경에서도 UI 일관성 유지

### Write 작업 안정화(재진입 방지)

- 일정 Add / Edit / Delete 중 중복 실행 가능
- IsBusy 상태 기반으로 Command 실행 제어
- 처리 중에는 버튼 비활성화 및 로딩 오버레이 표시

 -> 중복 실행, 연타, 상태 꼬임 방지

### 동시성 개선

- 동일한 일정을 여러 창에서 동시에 수정할 수 있는 상황으로 가정
- 저장 시점이 다른 수정이 먼저 반영되는 경우 기존 변경 사항이 인지되지 않은 채 덮어써질 수 있는 문제 발생 가능

**해결**

- RowVersion 기반 낙관적 동시성 제어 적용하여 저장 시점에 데이터 변경 여부 확인
- 다른 수정으로 인해 충돌이 감지되면 저장을 중단하고 최신 데이터로 화면 갱신

 -> 예상하지 못한 덮어쓰기 방지하고 정확한 데이터 상태 확인 가능

### 클라우드 배포 대응 시간 처리

- 로컬과 클라우드(AWS/Azure) 환경 간 서버 시간 차이로 인한 오차 존재

**해결**

- 일정은 UTC 저장하고 화면 표시나 사용자 입력은 KST 처리
- 서비스 계층에서 UTC와 KST 변환 적용

 -> 배포 환경과 무관하게 일관된 시간 기준으로 일정 관리 가능

### 검색 및 필터

- 일정 수가 많을수록 탐색 비용 증가

**해결**

- 일정 목록은 날짜 변경 시에만 DB 조회
- 검색 및 필터는 ViewModel의 ObservableCollection을 활용하여 빠른 화면 반응성과 불필요한 DB 접근 방지
