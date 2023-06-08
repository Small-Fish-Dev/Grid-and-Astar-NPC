﻿namespace GridAStar;

public static partial class TraceExtensions
{

	public static Sandbox.Trace WithGridSettings( this Sandbox.Trace self, GridBuilder settings )
	{
		self.WithAllTags( settings.TagsToInclude.ToArray() )
			.WithoutTags( settings.TagsToExclude.ToArray() );

		if ( settings.WorldOnly )
			self.WorldOnly();
		else
			self.WorldAndEntities();

		return self;
	}

}
