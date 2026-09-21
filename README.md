# Actra

**Your PC. Just ask.**

Actra는 Windows에서 자연어로 파일을 찾고, 앱을 실행하고, 시스템 작업을 수행하는 **Local AI Command Center**를 목표로 하는 오픈소스 프로젝트입니다.

> 현재 단계: Windows MVP

## 핵심 방향

- **Fast Path**: 명확한 명령은 LLM 없이 즉시 실행
- **Local Router**: 애매한 자연어는 로컬 LLM(Ollama)로 intent/slot 변환
- **Capability 기반 실행**: AI가 직접 PC를 조작하지 않고, 허용된 기능만 코드가 실행
- **Local-first**: 가능한 처리는 PC 안에서 수행

## MVP 기능

- `Ctrl + Space` 글로벌 런처
- 앱 실행
- 파일 검색
- 계산기/설정 등 빠른 실행
- 자연어 Intent Router
- Ollama 연동(선택)
- 실행 결과 표시

## 기술 스택

- C# / .NET 8
- WPF
- Win32 Global Hotkey
- System.Text.Json
- Ollama HTTP API (선택)

## 구조

```
User Query
   |
   v
FastPath Router
   |
   +-- matched --> Capability Executor
   |
   +-- unknown --> Local LLM Router
                     |
                     v
                Intent + Slots
                     |
                     v
               Capability Executor
```

## 실행

Windows 10/11과 .NET 8 SDK가 필요합니다.

```powershell
dotnet restore
dotnet run --project src/Actra.App
```

Ollama 기반 자연어 라우팅을 사용하려면 Ollama를 실행한 뒤 작은 로컬 모델을 준비합니다.

```powershell
ollama pull qwen2.5:0.5b
```

Actra는 Ollama가 없어도 Fast Path 기능은 동작하도록 설계합니다.

## License

추후 결정
