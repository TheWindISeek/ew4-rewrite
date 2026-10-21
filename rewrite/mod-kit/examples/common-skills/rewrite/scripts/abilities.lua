function blast(ctx)
    return { value = ctx.value * ctx.level, state = { hits = (ctx.state.hits or 0) + 1 } }
end

function poison_tick(ctx)
    return { value = ctx.value * ctx.stacks, state = { ticks = (ctx.state.ticks or 0) + 1 } }
end
