# Rearchitect your code towards async/await

The die is cast. Your business- and infrastructure logic will need to move gradually to asynchronous code. Changing large code-bases to async/await seems like a monumental effort. It doesn't have to be! In this talk, I show you a four-phased approach to evolving your code-base towards async/wait. In the identification phase, we classify the components which would benefit from async/await. In the exploration phase, we discover potential road blockers which might hinder the async/await adoption.

In the obstacle phase, we learn to redesign parts of the code to remove the previously identified road blockers. In the bring-it-together phase, we gradually move the components which benefit the most from async/await to a full asynchronous API. Small steps. No Big Bang. Join me on a journey towards async/await and learn to leverage the power of asynchronous code to embrace the cloud-first APIs of today and tomorrow.

Please read the [LICENSE.md](License) agreement

The font used in the slides is

[Kaffeesatz](https://www.yanone.de/fonts/kaffeesatz/)

# Links
## About me
* [Geeking out with Daniel Marbach]( http://developeronfire.com/episode-077-daniel-marbach-geeking-out)

## Async / Await
* [Six Essential Tips for Async](http://channel9.msdn.com/Series/Three-Essential-Tips-for-Async)
* [Asynchronous Programming with Async and Await](https://msdn.microsoft.com/en-us/library/hh191443.aspx)
* [Async/Await - Best Practices in Asynchronous Programming](https://msdn.microsoft.com/en-us/magazine/jj991977.aspx)
* [Async/Await FAQ](http://blogs.msdn.com/b/pfxteam/archive/2012/04/12/async-await-faq.aspx)
* [Should I expose synchronous wrappers for asynchronous methods?](http://blogs.msdn.com/b/pfxteam/archive/2012/04/13/10293638.aspx)
* [Should I expose asynchronous wrappers for synchronous methods?](http://blogs.msdn.com/b/pfxteam/archive/2012/03/24/10287244.aspx)
* [Task Parallel Library](https://msdn.microsoft.com/en-us/library/dd460717.aspx)
* [`Task.Factory.StartNew` vs `Task.Run`](http://blogs.msdn.com/b/pfxteam/archive/2011/10/24/10229468.aspx)
* [ConfigureAwait Rosly Analyzer](https://github.com/Particular/Particular.CodeRules/tree/master/src/Particular.CodeRules/ConfigureAwait)
* [Async Fixer extension](https://visualstudiogallery.msdn.microsoft.com/03448836-db42-46b3-a5c7-5fc5d36a8308)
* [Sync vs. Async, IO bound and CPU bound discussion on ayende.com](https://ayende.com/blog/173473/fun-async-tricks-for-getting-better-performance)
* [Await statement inside lock](http://stackoverflow.com/questions/7612602/why-cant-i-use-the-await-operator-within-the-body-of-a-lock-statement)
* [AsyncEx](TODO)
* [CLR 4.0 Threadpool improvements](https://blogs.msdn.microsoft.com/ericeil/2009/04/23/clr-4-0-threadpool-improvements-part-1/)
* [Overview of the Threadpool](https://msdn.microsoft.com/en-us/magazine/ff960958.aspx)

## Dart
* [Asynchrony with Dart](https://www.dartlang.org/docs/dart-up-and-running/ch02.html#asynchrony)
* [Asynchronous Programming with Dart](https://www.dartlang.org/docs/dart-up-and-running/ch03.html#dartasync---asynchronous-programming)

## Javascript
* [ES7 async functions](https://jakearchibald.com/2014/es7-async-functions/)
* [Async Functions](https://tc39.github.io/ecmascript-asyncawait/)
* [Async/await for ECMAScript](https://github.com/tc39/ecmascript-asyncawait)
* [BabelJS Syntax for async functions](https://babeljs.io/docs/plugins/syntax-async-functions/)
* [BabelJS Regenerator transform](http://babeljs.io/docs/plugins/transform-regenerator/)
* [BabelJS async to generator transform](http://babeljs.io/docs/plugins/transform-async-to-generator/)
* [BabelJS async function to module method transform](http://babeljs.io/docs/plugins/transform-async-to-module-method/)

## Python
* [Asynchronous I/O, event loop, coroutines and tasks](https://docs.python.org/3/library/asyncio.html)
* [Tasks and coroutines](https://docs.python.org/3/library/asyncio-task.html)

## Particular
* [End to End Performance tests](https://github.com/Particular/EndToEnd/tree/master/src/PerformanceTests)
* [Highlevel Spike RabbitMQ Client](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/151)
* [Lowlevel Spike RabbitMQ Client](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/149)
