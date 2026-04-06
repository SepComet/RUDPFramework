## 1. Run loopback validation test

- [x] 1.1 Start Unity Editor with server + client in loopback mode
- [x] 1.2 Hold steady turn-and-move input (e.g., turn=0.5, throttle=1) for 10+ seconds
- [x] 1.3 Observe MainUI correction text (校正：pos差=X rot差=Y°) — record observed values

## 2. Evaluate results

- [x] 2.1 If pos差 < 0.01 and rot差 < 1° consistently: jitter is resolved, proceed to Step 5
- [x] 2.2 If corrections remain large or jitter is still visible: document residual error for Step 5

**观察结果：** 抖动仍然明显（corrections 仍然较大），需要 Step 5 进一步诊断和回归覆盖。

## 3. Complete

- [x] 3.1 Mark TODO.md Step 4 as complete
