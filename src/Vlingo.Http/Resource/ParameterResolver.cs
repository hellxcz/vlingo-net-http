﻿// Copyright (c) 2012-2019 Vaughn Vernon. All rights reserved.
//
// This Source Code Form is subject to the terms of the
// Mozilla Public License, v. 2.0. If a copy of the MPL
// was not distributed with this file, You can obtain
// one at https://mozilla.org/MPL/2.0/.

using System;
using Vlingo.Http.Media;

namespace Vlingo.Http.Resource
{
    internal interface IParameterResolver
    {
        ParameterResolver.Type Type { get; }
        Type ParamClass { get; }
        object Apply(Request request, Action.MappedParameters mappedParameters);
    }

    internal class ParameterResolver<T> : IParameterResolver
    {
        public ParameterResolver.Type Type { get; }
        public Type ParamClass { get; }
        private readonly Func<Request, Action.MappedParameters, T> _resolver;

        private ParameterResolver(ParameterResolver.Type type, Type paramClass, Func<Request, Action.MappedParameters, T> resolver)
        {
            Type = type;
            ParamClass = paramClass;
            _resolver = resolver;
        }

        object IParameterResolver.Apply(Request request, Action.MappedParameters mappedParameters)
            => _resolver.Invoke(request, mappedParameters);

        public T Apply(Request request, Action.MappedParameters mappedParameters)
            => _resolver.Invoke(request, mappedParameters);

        internal static ParameterResolver<T> Create(ParameterResolver.Type type, Type paramClass, Func<Request, Action.MappedParameters, T> resolver)
            => new ParameterResolver<T>(type, paramClass, resolver);
    }

    internal static class ParameterResolver
    {
        public static ParameterResolver<T> Path<T>(int position, System.Type paramClass)
            => ParameterResolver<T>.Create(
                Type.PATH,
                paramClass,
                (request, mappedParameters) =>
                {
                    var value = mappedParameters.Mapped[position].Value;
                    if (paramClass.IsInstanceOfType(value))
                    {
                        return (T)value!;
                    }

                    throw new ArgumentException("Value " + value + " is of mimeType " + mappedParameters.Mapped[position].Type + " instead of " + paramClass.Name);
                });

        public static ParameterResolver<T> Body<T>(System.Type bodyClass)
            => Body<T>(bodyClass, DefaultMediaTypeMapper.Instance);

        public static ParameterResolver<T> Body<T>(System.Type bodyClass, IMapper mapper)
            => ParameterResolver<T>.Create(
                Type.BODY,
                bodyClass,
                (request, mappedParameters) => (T)mapper.From(request?.Body?.ToString(), bodyClass)!);

        public static ParameterResolver<T> Body<T>(System.Type bodyClass, MediaTypeMapper mediaTypeMapper)
            => ParameterResolver<T>.Create(
                Type.BODY,
                bodyClass,
                (request, mappedParameters) =>
                {
                    var assumedBodyContentType = ContentMediaType.Json.ToString();
                    var bodyMediaType = request.HeaderValueOr(RequestHeader.ContentType, assumedBodyContentType);
                    return mediaTypeMapper.From<T>(request?.Body?.ToString(), ContentMediaType.ParseFromDescriptor(bodyMediaType), bodyClass);
                });

        public static ParameterResolver<Header> Header(string headerName)
            => ParameterResolver<Header>.Create(
                Type.HEADER,
                typeof(Header),
                (request, mappedParameters) => request.HeaderOf(headerName));

        public static ParameterResolver<T> Query<T>(string name)
            => Query(name, typeof(string), default(T)!);

        public static ParameterResolver<T> Query<T>(string name, System.Type type)
            => Query(name, type, default(T)!);

        public static ParameterResolver<T> Query<T>(string name, System.Type type, T defaultValue)
            => ParameterResolver<T>.Create(
                Type.QUERY,
                type,
                (request, mappedParameters) =>
                {
                    string? value;
                    try
                    {
                        value = request.QueryParameters.ValuesOf(name)?[0];
                    }
                    catch (ArgumentException)
                    {
                        return defaultValue;
                    }
                    catch (NullReferenceException)
                    {
                        return defaultValue;
                    }

                    if (type == typeof(int))
                    {
                        return (T)(object)int.Parse(value);
                    }
                    else if (type == typeof(string))
                    {
                        return (T)(object)value!;
                    }
                    else if (type == typeof(float))
                    {
                        return (T)(object)float.Parse(value);
                    }
                    else if (type == typeof(long))
                    {
                        return (T)(object)long.Parse(value);
                    }
                    else if (type == typeof(bool))
                    {
                        return (T)(object)bool.Parse(value);
                    }
                    else if (type == typeof(short))
                    {
                        return (T)(object)short.Parse(value);
                    }
                    else if (type == typeof(byte))
                    {
                        return (T)(object)byte.Parse(value);
                    }
                    throw new ArgumentException("unknown mimeType " + type.Name);
                });

        internal enum Type
        {
            PATH,
            BODY,
            HEADER,
            QUERY
        }
    }
}
