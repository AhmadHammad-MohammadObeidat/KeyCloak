using MediatR;

namespace KeyCloak.Application.Messaging;

public interface IQuery<TResult> : IRequest<TResult> { }

public interface IQueryHandler<TQuery, TResult> : IRequestHandler<TQuery, TResult>
    where TQuery : IQuery<TResult>
{ }