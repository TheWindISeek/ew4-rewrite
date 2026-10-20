function repair(ctx)
    return {
        value = ctx.value * ctx.level,
        state = { uses = (ctx.state.uses or 0) + 1 }
    }
end

function siege(ctx)
    if ctx.target.category == "fort" then return 1.25 end
    return 1
end
