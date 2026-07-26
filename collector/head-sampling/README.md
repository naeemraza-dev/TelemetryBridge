# Head sampling

Set `OTEL_TRACES_SAMPLER` and `OTEL_TRACES_SAMPLER_ARG` on each workload:

```text
OTEL_TRACES_SAMPLER=parentbased_traceidratio
OTEL_TRACES_SAMPLER_ARG=0.10
```

Example starting points are development/test 100%, staging 25%, and production
5–10%. These are capacity-planning examples, not universal defaults.
