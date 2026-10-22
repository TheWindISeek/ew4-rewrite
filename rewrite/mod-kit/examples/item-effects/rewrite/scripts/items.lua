function ward(ctx)
    local uses = (ctx.state.uses or 0) + 1
    return {value = ctx.value * ctx.level, state = {uses = uses}}
end

function medicine(ctx)
    local uses = (ctx.state.uses or 0) + 1
    return {value = ctx.value * ctx.level + uses, state = {uses = uses}}
end
