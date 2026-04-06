# client-send-interval-stabilization Specification

## Purpose

Define that the client send interval is protected from oscillation when the server tick offset hovers near zero, ensuring steady-rate input submission without frame-to-frame cadence jitter.

## Requirements

### Requirement: Send interval correction uses hysteresis dead-band

The controlled-client send interval corrector SHALL apply a dead-band threshold before adjusting `_sendInterval`, so that minor server tick offset fluctuations near zero do not cause the send cadence to toggle between values.

#### Scenario: No correction within dead-band
- **WHEN** `_currentTickOffset` is between -2 and +2 ticks (inclusive)
- **THEN** `_sendInterval` is not changed
- **THEN** the previously active send interval is preserved

#### Scenario: Slow drift correction below threshold
- **WHEN** `_currentTickOffset` stays within the dead-band for an extended period
- **THEN** `_sendInterval` remains stable at its current value
- **THEN** no oscillation occurs regardless of offset sign changes within the band

#### Scenario: Correction applies outside dead-band
- **WHEN** `_currentTickOffset` exceeds +2 (client ahead of server)
- **THEN** `_sendInterval` is set to 0.048f to send slightly faster
- **WHEN** `_currentTickOffset` is below -2 (client behind server)
- **THEN** `_sendInterval` is set to 0.052f to send slightly slower

### Requirement: Send interval stabilizes after offset crosses threshold

Once the offset exits the dead-band and triggers a correction, subsequent corrections SHALL only occur when the offset crosses the threshold again in the opposite direction, preventing rapid re-correction.

#### Scenario: Correction latches until opposite threshold
- **WHEN** offset triggers a correction to 0.048f (offset > +2)
- **THEN** further offset increases within the same sign do not re-trigger correction
- **THEN** the send interval stays at 0.048f until offset crosses back below +2 then exceeds -2
